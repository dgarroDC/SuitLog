using System;
using System.Collections.Generic;
using System.Linq;
using OWML.Common;
using OWML.ModHelper;
using SuitLog.API;
using UnityEngine;
using UnityEngine.UI;

// TODO: show missing entries no animation desc field (what?)
// TODO: show missing facts?
namespace SuitLog
{
    public class SuitLog : ModBehaviour
    {
        public static SuitLog Instance;

        public List<SuitLogItemList> ItemLists = new(); // Kinda weird...

        private Dictionary<ShipLogMode, Tuple<Func<bool>, Func<string>>> _modes = new();
        private ShipLogMode _currentMode;

        private bool _setupDone;
        private bool _open;

        private SuitLogMode _suitLogMode;
        private ModeSelectorMode _modeSelectorMode;
        private ShipLogMode _requestedChangeMode;

        private ToolModeSwapper _toolModeSwapper;

        private OWAudioSource _oneShotSource;
        private ScreenPromptList _centerPromptList;
        private ScreenPromptList _upperRightPromptList;
        private RectTransform _upperRightPromptsRect;
        private ScreenPrompt _openPrompt;
        private ScreenPrompt _closePrompt;
        private ScreenPrompt _modeSelectorPrompt;
        private ScreenPrompt _modeSwapPrompt;

        internal const float OpenAnimationDuration = 0.13f;
        internal const float CloseAnimationDuration = 0.3f;

        private HashSet<object> _locks = new();

        private void Start()
        {
            Instance = this;
            ModHelper.HarmonyHelper.AddPrefix<BaseInputManager>("ChangeInputMode", typeof(SuitLog), nameof(ChangeInputModePrefixPatch));
            // Setup after ShipLogController.LateInitialize to find all ShipLogAstroObject added by New Horizons in ShipLogMapMode.Initialize postfix
            ModHelper.HarmonyHelper.AddPostfix<ShipLogController>("LateInitialize", typeof(SuitLog), nameof(SetupPatch));
            LoadManager.OnStartSceneLoad += OnStartSceneLoad;
        }

        public override object GetApi() {
            return new SuitLogAPI();
        }

        private void OnStartSceneLoad(OWScene scene, OWScene loadScene)
        {
            // This should be called before SetupPatch, see also considerations in CSLM
           _setupDone = false;
        }

        private static void SetupPatch(ShipLogController __instance)
        {
            Instance.Setup(__instance._mapMode as ShipLogMapMode);
        }

        // TODO: separate method that takes the map mode, or controller??
        private void Setup(ShipLogMapMode mapMode)
        {
            _open = false;

            _toolModeSwapper = Locator.GetToolModeSwapper();
            _centerPromptList = Locator.GetPromptManager().GetScreenPromptList(PromptPosition.BottomCenter); // Don't use the center one
            _upperRightPromptList = Locator.GetPromptManager().GetScreenPromptList(PromptPosition.UpperRight);
            _upperRightPromptsRect =  _upperRightPromptList.GetComponent<RectTransform>();

            ISuitLogAPI API = GetApi() as ISuitLogAPI;
            SuitLogItemList.CreatePrefab(_upperRightPromptList);
            API.ItemListMake(itemList =>
            {
                _oneShotSource = ((SuitLogItemList)itemList).oneShotSource; // This is shared
                
                _suitLogMode = itemList.gameObject.AddComponent<SuitLogMode>();
                _suitLogMode.itemList = new ItemListWrapper(API, itemList);
                _suitLogMode.shipLogMap = mapMode;
                _suitLogMode.name = nameof(SuitLogMode);
                InitializeMode(_suitLogMode); // Don't add it to _modes to force it being the first
                _currentMode = _suitLogMode;

                // Maybe both could share the same item lists? I don't know, this seems more consistent
                API.ItemListMake(itemList2 =>
                {
                    _modeSelectorMode = itemList2.gameObject.AddComponent<ModeSelectorMode>();
                    _modeSelectorMode.itemList = new ItemListWrapper(API, itemList2);
                    _modeSelectorMode.name = nameof(ModeSelectorMode);
                    InitializeMode(_modeSelectorMode); // Obviously not added to _modes
                    
                    SetupPrompts();
                    _setupDone = true;

                    // Initialize all already added modes, even disabled ones
                    foreach (ShipLogMode mode in _modes.Keys)
                    {
                        if (mode != null)
                        {
                            InitializeMode(mode);
                        }
                    }
                });
            });
        }

        private void Update()
        {
            if (!_setupDone) return;
            // TODO: Add option to pause when helmet is fully on, but not if end times is playing
            if (IsPauseMenuOpen())
            {
                // Setup is done, we can reference the prompts
                if (_open)
                {
                    // Really hacky...
                    SetPromptsPosition(-1000000);
                }
                else
                {
                    // It's the only one that could be visible while closed, so just hide it (and that way we don't affect other prompts, just in case...)
                    _openPrompt.SetVisibility(false);
                }
                return;
            }

            ShipLogMode nextMode = null;
            if (IsSuitLogOpenable())
            {
                if (Input.IsNewlyPressed(Input.Action.OpenSuitLog))
                {
                    OpenSuitLog();
                }
            }
            else if (_open)
            {
                if ((_currentMode.AllowCancelInput() && Input.IsNewlyPressed(Input.Action.CloseSuitLog)) || !CanSuitLogRemainOpen())
                {
                    CloseSuitLog();
                }
                else
                {
                    _currentMode.UpdateMode();
                    // Kinda like UpdatePromptsVisibility+UpdateChangeMode from CSLM but simpler?
                    if (_requestedChangeMode != null)
                    {
                        ChangeMode(_requestedChangeMode);
                        _requestedChangeMode = null;
                    }
                    else
                    {
                        List<Tuple<ShipLogMode, string>> availableNamedModes = GetAvailableNamedModes();
                        int currentModeIndex = availableNamedModes.FindIndex(m => m.Item1 == _currentMode);  // idk about the >= 0, from CSLM... (although not affecting selector there?)
                        if (currentModeIndex == -1 && _currentMode != _modeSelectorMode)
                        {
                            // Same CSLM trap case, although the check is different, but the index seems enough
                            ChangeMode(_suitLogMode);
                        }
                        else if (_currentMode.AllowModeSwap() && availableNamedModes.Count >= 2)
                        {
                            nextMode = availableNamedModes[(currentModeIndex + 1) % availableNamedModes.Count].Item1; // Calculate here to use it for prompt visibility?
                            if (Input.IsNewlyPressed(Input.Action.OpenModeSelector))
                            {
                                _modeSelectorMode.SetGoBackMode(_currentMode);
                                ChangeMode(_modeSelectorMode);
                            }
                            else if (Input.IsNewlyPressed(Input.Action.SwapMode))
                            {
                                ChangeMode(nextMode);
                            }
                        }
                    }
                }
            }

            UpdatePromptsVisibility(nextMode); // The mode could be one frame "delayed"
        }

        public static bool IsPauseMenuOpen()
        {
            return Locator.GetSceneMenuManager().pauseMenu.IsOpen();
        }
        
        private void SetPromptsPosition(float positionY)
        {
            // See PromptManager: Lower the prompts when the image is displayed
            Vector2 anchoredPosition = _upperRightPromptsRect.anchoredPosition;
            anchoredPosition.y = positionY;
            _upperRightPromptsRect.anchoredPosition = anchoredPosition;
        }

        private void OpenSuitLog()
        {
            OWInput.ChangeInputMode(InputMode.None);
            _open = true;

            foreach ((ShipLogMode shipLogMode, _) in GetAvailableNamedModes()) // TODO: Also disabled?
            {
                shipLogMode.OnEnterComputer();
            }
            _currentMode.EnterMode();
        }
 
        private void CloseSuitLog()
        {
            _oneShotSource.PlayOneShot(AudioType.ShipLogDeselectPlanet); // Modes aren't supposed to play sound on exit, so we do it here
            _currentMode.ExitMode();
            foreach ((ShipLogMode shipLogMode, _) in GetAvailableNamedModes())
            {
                shipLogMode.OnExitComputer();
            }
 
            OWInput.RestorePreviousInputs(); // This should always be Character
            _open = false;
            SetPromptsPosition(0); // It's possible that it's still lowered when closing the suit log
        }

        private bool IsSuitLogOpenable()
        {
            return !_open &&
                   OWInput.IsInputMode(InputMode.Character) &&
                   CanSuitLogRemainOpen();
        }

        private bool CanSuitLogRemainOpen()
        {
            // Check for holding custom items which might have overlap with keybindings.
            bool holds_tool = false;
            if (_toolModeSwapper.GetToolMode() == ToolMode.Item)
            {
                OWItem item = _toolModeSwapper.GetItemCarryTool().GetHeldItem();
                // Ignore base game items.
                holds_tool = !(item is ScrollItem || item is SharedStone || item is DreamLanternItem || item is SimpleLanternItem ||
                    item is SlideReelItem || item is NomaiConversationStone || item is WarpCoreItem);
            }
            return Locator.GetPlayerSuit().IsWearingHelmet() &&
                   !Locator.GetPlayerSuit().IsTrainingSuit() &&
                   !_toolModeSwapper._signalscope.IsEquipped() &&
                   !_toolModeSwapper._probeLauncher.IsEquipped() &&
                   !_toolModeSwapper._translator.IsEquipped() &&
                   !holds_tool &&
                   _locks.Count == 0 &&
                   // Otherwise the repair prompt will appear in suit log and conflict with view entries
                   _toolModeSwapper._firstPersonManipulator._focusedRepairReceiver == null;
        }

        private void SetupPrompts()
        {
            _openPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.OpenSuitLog), TranslationHandler.GetTranslation("OPEN_SUIT_LOG"));
            _closePrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.CloseSuitLog), TranslationHandler.GetTranslation("CLOSE_SUIT_LOG"));
            _modeSelectorPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.OpenModeSelector), ModeSelectorMode.GetName());
            _modeSwapPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.SwapMode), ""); // The text is updated
            Locator.GetPromptManager().AddScreenPrompt(_openPrompt, _upperRightPromptList, TextAnchor.MiddleRight);
            Locator.GetPromptManager().AddScreenPrompt(_closePrompt, _upperRightPromptList, TextAnchor.MiddleRight);
            Locator.GetPromptManager().AddScreenPrompt(_modeSelectorPrompt, _upperRightPromptList, TextAnchor.MiddleRight);
            Locator.GetPromptManager().AddScreenPrompt(_modeSwapPrompt, _upperRightPromptList, TextAnchor.MiddleRight);
        }

        private void UpdatePromptsVisibility(ShipLogMode nextMode)
        {
            _openPrompt.SetVisibility(IsSuitLogOpenable());
            _closePrompt.SetVisibility(_open && _currentMode.AllowCancelInput()); // Maybe _open not needed???
            _modeSelectorPrompt.SetVisibility(_open && nextMode != null);
            _modeSwapPrompt.SetVisibility(_open && nextMode != null);
            if (nextMode != null)
            {
                List<Tuple<ShipLogMode, string>> availableNamedModes = GetAvailableNamedModes();
                string nextModeName = availableNamedModes.Find(m => m.Item1 == nextMode).Item2;
                _modeSwapPrompt.SetText(nextModeName);
            }

            if (_open)
            {
                bool shouldLowerPrompts = false;
                foreach (SuitLogItemList itemList in ItemLists)
                {
                    if (itemList != null && itemList.IsOpen && (itemList.photo.gameObject.activeSelf || itemList.questionMark.gameObject.activeSelf))
                    { 
                        shouldLowerPrompts = true;
                        break;
                    }
                }
                if (shouldLowerPrompts)
                {
                    SetPromptsPosition(-250f);
                }
                else
                {
                    SetPromptsPosition(0f);
                }
            }
            // Don't handle close here, because probe launcher could change it, reset on close
        }

        public void AddMode(ShipLogMode mode, Func<bool> isEnabledSupplier, Func<string> nameSupplier)
        {
            if (_modes.ContainsKey(mode))
            {
                ModHelper.Console.WriteLine("Mode " + mode + " already added, replacing suppliers...", MessageType.Info);
            }
            _modes[mode] = new Tuple<Func<bool>, Func<string>>(isEnabledSupplier, nameSupplier);

            if (_setupDone)
            {
                InitializeMode(mode);
            }
        }
        
        private void InitializeMode(ShipLogMode mode)
        {
            mode.Initialize(_centerPromptList, _upperRightPromptList, _oneShotSource);
        }
        
        private void ChangeMode(ShipLogMode enteringMode)
        {
            ShipLogMode leavingMode = _currentMode;
            string focusedEntryID = leavingMode.GetFocusedEntryID();
            leavingMode.ExitMode();
            _currentMode = enteringMode;
            _currentMode.EnterMode(focusedEntryID);
        }
        
        public void RequestChangeMode(ShipLogMode mode)
        {
            // Same considerations as CSLM, although there isn't a postfix here so not sure, just in case...
            if (_requestedChangeMode == null)
            {
                _requestedChangeMode = mode;
            } 
        }

        public List<Tuple<ShipLogMode, string>> GetAvailableNamedModes()
        {
            // TODO: Cache per update?
            // Probably GetCustomModes() not needed in this mod
            List<Tuple<ShipLogMode, string>> modes = _modes
                .Where(mode => mode.Key != null && mode.Value.Item1.Invoke())
                .Select(mode => new Tuple<ShipLogMode, string>(mode.Key, mode.Value.Item2.Invoke()))
                .OrderBy(mode => mode.Item2)
                .ToList();

            modes.Insert(0, new Tuple<ShipLogMode, string>(_suitLogMode, SuitLogMode.GetName())); 
            
            return modes;
        }

        internal static void SetParent(Transform child, Transform parent)
        {
            child.parent = parent;
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
        }

        internal static Text CreateText()
        {
            GameObject template = GameObject.Find("PlayerHUD/HelmetOnUI/UICanvas/SecondaryGroup/GForce/NumericalReadout/GravityText");
            GameObject textObject = Instantiate(template);
            Destroy(textObject.GetComponent<LocalizedText>());
            textObject.name = "Text";
            Text text = textObject.GetComponent<Text>();
            text.text = "If you're reading this, this is a bug, please report it!";
            text.alignment = TextAnchor.UpperLeft;
            text.fontSize = 26;
            text.gameObject.SetActive(false);
            return text;
        }

        private static void ChangeInputModePrefixPatch(ref InputMode mode) {
            if (Instance._open && mode != InputMode.Menu)
            {
                // This is done because the input mode on vision, projection, remote conversation, death, memory uplink...
                // And in some cases then the previous input mode is restored an in others it changes to Character
                // But don't close it if pausing game
                // (going to Menu is also possible in preflight list but Suit Log should be closed when doing that)
                Instance.CloseSuitLog();
            }
        }

        public void LockSuitLog(object caller)
        {
            _locks.Add(caller);
        }

        public void UnlockSuitLog(object caller)
        {
            _locks.Remove(caller);
        }
    }
}
