using OWML.ModHelper;
using UnityEngine;
using UnityEngine.UI;

// TODO: show missing entries no animation desc field
// TODO: show missing facts?
namespace SuitLog
{
    public class SuitLog : ModBehaviour
    {
        public static SuitLog Instance;

        private bool _setupDone;
        private bool _open;
        private SuitLogItemList _itemlist;
        private SuitLogMode _suitLogMode;

        private ToolModeSwapper _toolModeSwapper;
 
        private ScreenPromptList _upperRightPromptList;
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
            _toolModeSwapper = Locator.GetToolModeSwapper();
            _upperRightPromptList = Locator.GetPromptManager().GetScreenPromptList(PromptPosition.UpperRight);

            _itemlist = SuitLogItemList.Create(_upperRightPromptList);
            _suitLogMode = _itemlist.gameObject.AddComponent<SuitLogMode>();
            _suitLogMode.itemList = _itemlist;
            _suitLogMode.shipLogMap = mapMode;
            _suitLogMode.Initialize(null, _upperRightPromptList, _itemlist.oneShotSource);
            SetupPrompts();
            
            _open = false;
            _setupDone = true;
        }

        private void Update()
        {
            if (!_setupDone) return;
            // TODO: Add option to pause when helmet is fully on, but not if end times is playing
            if (Locator.GetSceneMenuManager().pauseMenu.IsOpen())
            {
                // Setup is done, we can reference the prompts
                HideAllPrompts();
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

        private void OpenSuitLog()
        {
            _suitLogMode.EnterMode();

            // TODO: Move this check to map mode check if available?
            if (_itemlist.contentsItems.Count > 0)
            {
                _itemlist.Open();
                OWInput.ChangeInputMode(InputMode.None);
                _open = true;
            }
            else
            {
                // TODO: What about custom modes??? Open custom mode if map mode empty, if no custom mode available, display this notif??
                // TODO: Translation
                // This case shouldn't be possible in vanilla because the fact TH_VILLAGE_X1 is always revealed, this was added for New Horizons
                NotificationData notification = new NotificationData(NotificationTarget.Player, "SUIT LOG IS EMPTY");
                NotificationManager.SharedInstance.PostNotification(notification);
            }
        }
 
        private void CloseSuitLog()
        {
            _suitLogMode.ExitMode();
            OWInput.RestorePreviousInputs(); // This should always be Character
            _open = false;
            _itemlist.Close();
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
        }

        private void HideAllPrompts()
        {
            _openPrompt.SetVisibility(false);
            _closePrompt.SetVisibility(false);
            _itemlist.HideAllPrompts();
            _suitLogMode.HideAllPrompts(); // TODO: Suggest other mods do the same! API utility?? (listener??)
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
