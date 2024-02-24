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

        private ToolModeSwapper _toolModeSwapper;

        private OWAudioSource _oneShotSource;
        private ScreenPromptList _upperRightPromptList;
        private RectTransform _upperRightPromptsRect;
        private ScreenPrompt _openPrompt;
        private ScreenPrompt _closePrompt;

        internal const float OpenAnimationDuration = 0.13f;
        internal const float CloseAnimationDuration = 0.3f;

        private void Start()
        {
            Instance = this;
            ModHelper.HarmonyHelper.AddPrefix<BaseInputManager>("ChangeInputMode", typeof(SuitLog), nameof(ChangeInputModePrefixPatch));
            // Setup after ShipLogController.LateInitialize to find all ShipLogAstroObject added by New Horizons in ShipLogMapMode.Initialize postfix
            ModHelper.HarmonyHelper.AddPostfix<ShipLogController>("LateInitialize", typeof(SuitLog), nameof(SetupPatch));
            LoadManager.OnStartSceneLoad += OnStartSceneLoad;
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
            _upperRightPromptList = Locator.GetPromptManager().GetScreenPromptList(PromptPosition.UpperRight);
            _upperRightPromptsRect =  _upperRightPromptList.GetComponent<RectTransform>();

            ISuitLogAPI API = new SuitLogAPI();
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
                }
            }

            UpdatePromptsVisibility();
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
            _open = true;
            OWInput.ChangeInputMode(InputMode.None);

            foreach ((ShipLogMode shipLogMode, _) in GetAvailableNamedModes())
            {
                shipLogMode.OnEnterComputer();
            }
            _currentMode.EnterMode();
        }
 
        private void CloseSuitLog()
        {
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
            return Locator.GetPlayerSuit().IsWearingHelmet() &&
                   !Locator.GetPlayerSuit().IsTrainingSuit() &&
                   !_toolModeSwapper._signalscope.IsEquipped() &&
                   !_toolModeSwapper._probeLauncher.IsEquipped() &&
                   !_toolModeSwapper._translator.IsEquipped() &&
                   // Otherwise the repair prompt will appear in suit log and conflict with view entries
                   _toolModeSwapper._firstPersonManipulator._focusedRepairReceiver == null;
        }

        private void SetupPrompts()
        {
            // TODO: Translations
            _openPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.OpenSuitLog), "Open Suit Log");
            _closePrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.CloseSuitLog), "Close Suit Log");
            Locator.GetPromptManager().AddScreenPrompt(_openPrompt, PromptPosition.UpperRight);
            Locator.GetPromptManager().AddScreenPrompt(_closePrompt, PromptPosition.UpperRight);
        }

        private void UpdatePromptsVisibility()
        {
            _openPrompt.SetVisibility(IsSuitLogOpenable());
            _closePrompt.SetVisibility(_open && _currentMode.AllowCancelInput()); // Maybe _open not needed???

            if (_open)
            {
                bool shouldLowerPrompts = false;
                foreach (SuitLogItemList itemList in ItemLists)
                {
                    if (itemList != null && (itemList.photo.gameObject.activeSelf || itemList.questionMark.gameObject.activeSelf))
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

            if (!_setupDone)
            {
                InitializeMode(mode);
            }
        }
        
        private void InitializeMode(ShipLogMode mode)
        {
            mode.Initialize(null, _upperRightPromptList, _oneShotSource);
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

            modes.Insert(0, new Tuple<ShipLogMode, string>(_suitLogMode, SuitLogMode.Name)); 
            
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
    }
}
