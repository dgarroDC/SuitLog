using System.Collections.Generic;
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
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        private void OnCompleteSceneLoad(OWScene scene, OWScene loadScene)
        {
            // This should be called before SetupPatch
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

            SuitLogItemList.CreatePrefab(_upperRightPromptList);
            SuitLogItemList.Make(itemList =>
            {
                _oneShotSource = ((SuitLogItemList)itemList).oneShotSource; // This is shared
                
                _suitLogMode = itemList.gameObject.AddComponent<SuitLogMode>();
                _suitLogMode.itemList = new ItemListWrapper(new SuitLogAPI(), itemList);
                _suitLogMode.shipLogMap = mapMode;
                _suitLogMode.name = nameof(SuitLogMode);
                _suitLogMode.Initialize(null, _upperRightPromptList, _oneShotSource);

                SetupPrompts();
                _setupDone = true;
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
                if ((_suitLogMode.AllowCancelInput() && Input.IsNewlyPressed(Input.Action.CloseSuitLog)) || !CanSuitLogRemainOpen())
                {
                    CloseSuitLog();
                }
                else
                {
                    _suitLogMode.UpdateMode();
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
            _suitLogMode.EnterMode();
            OWInput.ChangeInputMode(InputMode.None);
            _open = true;
        }
 
        private void CloseSuitLog()
        {
            _suitLogMode.ExitMode();
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
            _closePrompt.SetVisibility(_open && _suitLogMode.AllowCancelInput()); // Maybe _open not needed???

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
