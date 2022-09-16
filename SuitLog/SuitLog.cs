using System.Collections.Generic;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;
using UnityEngine.UI;

namespace SuitLog
{
    public class SuitLog : ModBehaviour
    {
        private static SuitLog _instance;

        private bool _enabled;
        private bool _setupDone;
        private bool _open;
        private bool _isEntryMenuOpen; // <=> _entryItems not empty
        private GameObject _suitLog;
        private OWAudioSource _audioSource;
        private DescriptionField _descField;
        private ListNavigator _listNavigator;

        private ToolModeSwapper _toolModeSwapper;
        private ShipLogManager _shipLogManager;
        private ShipLogEntryHUDMarker _entryHUDMarker;
        private Image _photo;
        private RectTransform _topRightPrompts;

        private Dictionary<string,ShipLogAstroObject> _shipLogAstroObjects;
        private HashSet<string> _astroObjectIds;

        private const int ListSize = 10;
        private Text[] _textList;
        private List<ListItem> _items = new();
        private List<ShipLogEntry> _entryItems = new();
        private int _selectedItem;
        private string _selectedAstroObjectID;
        private Text _titleText;

        private ScreenPrompt _openPrompt;
        private ScreenPrompt _closePrompt;
        private ScreenPrompt _viewEntriesPrompt;
        private ScreenPrompt _closeEntriesPrompt;
        private ScreenPrompt _markOnHUDPrompt;

        private CanvasGroupAnimator _suitLogAnimator;
        private CanvasGroupAnimator _notificationsAnimator;
        internal const float OpenAnimationDuration = 0.13f;
        internal const float CloseAnimationDuration = 0.3f;

        private void Start()
        {
            _instance = this;
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

        private static void SetupPatch()
        {
            _instance.Setup();
        }

        private void Setup()
        {
            _toolModeSwapper = Locator.GetToolModeSwapper();
            _shipLogManager = Locator.GetShipLogManager();
            _entryHUDMarker = FindObjectOfType<ShipLogEntryHUDMarker>();
            _photo = GameObject.Find("PlayerHUD/HelmetOnUI/UICanvas/HUDProbeDisplay/Image").GetComponent<Image>();
            _topRightPrompts = Locator.GetPromptManager().GetScreenPromptList(PromptPosition.UpperRight).GetComponent<RectTransform>();

            _shipLogAstroObjects = new Dictionary<string, ShipLogAstroObject>();
            ShipLogMapMode mapMode = Resources.FindObjectsOfTypeAll<ShipLogMapMode>()[0];
            foreach (ShipLogAstroObject[] astroObjects in mapMode._astroObjects)
            {
                foreach (ShipLogAstroObject astroObject in astroObjects)
                {
                    // We want to use the ShipLogAstroObject to use the GetName patched by New Horizons...
                    _shipLogAstroObjects.Add(astroObject.GetID(), astroObject);
                }
            }
            _astroObjectIds = new HashSet<string>();
            foreach (ShipLogEntry entry in _shipLogManager.GetEntryList())
            {
                // We only want to show these astro objects, also iterating this gives a nice order in stock planets (?
                string astroObjectID = entry.GetAstroObjectID();
                if (_shipLogAstroObjects.ContainsKey(astroObjectID))
                {
                    _astroObjectIds.Add(astroObjectID); 
                }
                else
                {
                    ModHelper.Console.WriteLine(
                        $"Entry {entry.GetID()} has an invalid astro object id {entry.GetAstroObjectID()}, " +
                        $"this may be an error in a New Horizons addon, please report this error!\n" +
                        $"The entry won't be shown in the Suit Log", MessageType.Error);
                }
            }

            SetupUI();
            _descField = new DescriptionField(_suitLog);
            _listNavigator = new ListNavigator();
            SetupPrompts();
            
            GameObject notificationAudio = GameObject.Find("Player_Body/Audio_Player/NotificationAudio");
            GameObject audioSourceObject = Instantiate(notificationAudio);
            SetParent(audioSourceObject.transform, _suitLog.transform);
            _audioSource = audioSourceObject.GetComponent<OWAudioSource>();

            // Don't use the one of the map mode because with New Horizons it could be an astro object not present
            // in the Suit Log (in vanilla is Timber Hearth that is always there), just select the first item...
            _selectedAstroObjectID = null; 
            _open = false;
            _isEntryMenuOpen = false;
            _setupDone = true;
        }

        private void Update()
        {
            if (!_setupDone) return;
            // TODO: Add option to pause when helmet is fully on, but not if end times is playing
            if (!_enabled && _open && !OWTime.IsPaused())
            {
                // Don't do this on Configure to not mess with the input mode (paused in menu)
                CloseSuitLog();
            }
            if (OWTime.IsPaused() || !_enabled)
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
                int selectionChange = _items.Count < 2 ? 0 : _listNavigator.GetSelectionChange();
                if ((!_isEntryMenuOpen && Input.IsNewlyPressed(Input.Action.CloseSuitLog)) || !CanSuitLogRemainOpen())
                {
                    CloseSuitLog();
                }
                else if (selectionChange != 0)
                {
                    int prevSelectedItem = _selectedItem;
                    _selectedItem += selectionChange;
                    if (_selectedItem == -1)
                    {
                        _selectedItem = _items.Count - 1;
                    }
                    else if (_selectedItem == _items.Count)
                    {
                        _selectedItem = 0;
                    }

                    PlayOneShot(AudioType.ShipLogMoveBetweenEntries);
                    string selectedItemId = _items[_selectedItem].id;
                    if (!_isEntryMenuOpen)
                    {
                        _selectedAstroObjectID = selectedItemId;
                    }
                    else
                    {
                        MarkAsRead(prevSelectedItem);
                        UpdateSelectedEntry();
                    }
                }
                else if (_isEntryMenuOpen && Input.IsNewlyPressed(Input.Action.MarkEntryOnHUD))
                {
                    ListItem item = _items[_selectedItem];
                    if (CanEntryBeMarkedOnHUD(_entryItems[_selectedItem]))
                    {
                        item.markedOnHUD = !item.markedOnHUD;
                        if (!item.markedOnHUD)
                        {
                            _entryHUDMarker.SetEntryLocation(null);
                            PlayOneShot(AudioType.ShipLogUnmarkLocation);
                        }
                        else
                        {
                            foreach (ListItem otherItem in _items)
                            {
                                if (otherItem != item)
                                {
                                    otherItem.markedOnHUD = false;
                                }
                            }
                            _entryHUDMarker.SetEntryLocation(Locator.GetEntryLocation(item.id));
                            PlayOneShot(AudioType.ShipLogMarkLocation);
                        }
                    }
                }
                else if (!_isEntryMenuOpen && Input.IsNewlyPressed(Input.Action.ViewEntries))
                {
                    OpenEntryMenu();
                }
                else if (_isEntryMenuOpen && Input.IsNewlyPressed(Input.Action.CloseEntries))
                {
                    CloseEntryMenu();
                }
            }

            // Another if because _open could have changed
            if (_open)
            {
                if (_isEntryMenuOpen)
                {
                    _descField.Update();
                }
                UpdateListUI();
            }

            UpdatePromptsVisibility();
        }

        public override void Configure(IModConfig config)
        {
            _enabled = config.Enabled;
        }

        private void OpenSuitLog()
        {
            LoadAstroObjectsMenu();

            if (_items.Count > 0)
            {
                OWInput.ChangeInputMode(InputMode.None);
                _open = true;
                _suitLogAnimator.AnimateTo(1, Vector3.one, OpenAnimationDuration);
                // Make notifications slightly transparent to avoid unreadable overlapping
                _notificationsAnimator.AnimateTo(0.35f, Vector3.one, OpenAnimationDuration);
                PlayOneShot(AudioType.ShipLogSelectPlanet);
            }
            else
            {
                // TODO: Translation
                // This case shouldn't be possible in vanilla because the fact TH_VILLAGE_X1 is always revealed, this was added for New Horizons
                NotificationData notification = new NotificationData(NotificationTarget.Player, "SUIT LOG IS EMPTY");
                NotificationManager.SharedInstance.PostNotification(notification);
            }
        }
 
        private void CloseSuitLog()
        {
            if (_isEntryMenuOpen)
            {
                CloseEntryMenu();
            }
            OWInput.RestorePreviousInputs(); // This should always be Character
            _open = false;
            _suitLogAnimator.AnimateTo(0, Vector3.one, CloseAnimationDuration);
            _notificationsAnimator.AnimateTo(1, Vector3.one, CloseAnimationDuration);
            PlayOneShot(AudioType.ShipLogDeselectPlanet);
        }

        private void OpenEntryMenu()
        {
            LoadEntriesMenu();
            _descField.Open();
            UpdateSelectedEntry();
            _isEntryMenuOpen = true;
            PlayOneShot(AudioType.ShipLogSelectPlanet);
        }

        private void CloseEntryMenu()
        {
            MarkAsRead(_selectedItem);
            LoadAstroObjectsMenu();
            _descField.Close();
            HidePhoto();
            _entryItems.Clear();
            _isEntryMenuOpen = false;
            PlayOneShot(AudioType.ShipLogDeselectPlanet);
        }
 
        private void UpdateSelectedEntry()
        {
            ShipLogEntry entry = _entryItems[_selectedItem];
            _descField.SetEntry(entry);
            if (entry.GetState() == ShipLogEntry.State.Explored)
            {
                ShowPhoto(entry);
            }
            else
            {
                HidePhoto();
            }
        }

        private void ShowPhoto(ShipLogEntry entry)
        {
            _photo.enabled = true;
            _photo.sprite = entry.GetSprite();
            SetPromptsPosition(-250f);
        }

        private void HidePhoto()
        {
            _photo.enabled = false;
            _photo.sprite = null;
            SetPromptsPosition(0f);
        }
 
        private void SetPromptsPosition(float positionY)
        {
            // See PromptManager: Lower the prompts when the image is displayed
            Vector2 anchoredPosition = _topRightPrompts.anchoredPosition;
            anchoredPosition.y = positionY;
            _topRightPrompts.anchoredPosition = anchoredPosition;
        }

        private void PlayOneShot(AudioType type)
        {
            _audioSource.PlayOneShot(type);
        }
        
        private void MarkAsRead(int index)
        {
            // TODO: setting to disable mark on read
            ListItem item = _items[index];
            if (item.unread)
            {
                ShipLogEntry entry = _entryItems[index];
                entry.MarkAsRead();
                item.unread = false;
            }
        }

        private void LoadAstroObjectsMenu()
        {
            _items.Clear();
            _selectedItem = 0; // Unnecessary statement probably...
            foreach (string astroObjectId in _astroObjectIds)
            {
                ShipLogAstroObject astroObject = _shipLogAstroObjects[astroObjectId];
                astroObject.OnEnterComputer();
                astroObject.UpdateState();
                ShipLogEntry.State state = astroObject.GetState(); 
                if (state != ShipLogEntry.State.Explored && state != ShipLogEntry.State.Rumored) continue;
                if (_selectedAstroObjectID == null)
                {
                    // This will make the first item to be selected the first time
                    _selectedAstroObjectID = astroObjectId;
                }
                if (astroObjectId == _selectedAstroObjectID)
                {
                    _selectedItem = _items.Count; // Next element to insert
                }
                _items.Add(new ListItem(
                    astroObjectId,
                    astroObject.GetName(),
                    state == ShipLogEntry.State.Rumored,
                    0,
                    astroObject._unviewedObj.activeSelf,
                    false,
                    false
                ));
            }

            _titleText.text = "Suit Log";
        }

        private void LoadEntriesMenu()
        {
            _selectedItem = 0;
            _items.Clear();
            _entryItems.Clear();
            ShipLogAstroObject selectedAstroObject = _shipLogAstroObjects[_selectedAstroObjectID];
            // Just in case the log was updated...
            selectedAstroObject.OnEnterComputer();
            // GetEntries would return reel entries added by ShipLogSlideReelPlayer, but who cares now
            List<ShipLogEntry> entries = selectedAstroObject.GetEntries();
            foreach (ShipLogEntry entry in entries)
            {
                if (entry.GetState() == ShipLogEntry.State.Explored || entry.GetState() == ShipLogEntry.State.Rumored)
                {
                    _items.Add(new ListItem(
                        entry.GetID(),
                        entry.GetName(false),
                        entry.GetState() == ShipLogEntry.State.Rumored,
                        GetEntryIndentation(entry),
                        entry.HasUnreadFacts(),
                        entry.HasMoreToExplore(),
                        IsEntryMarkedOnHUD(entry)
                    ));
                    _entryItems.Add(entry);
                }
            }

            _titleText.text = selectedAstroObject.GetName();
        }

        private int GetEntryIndentation(ShipLogEntry entry)
        {
            // This work even for more than one indentation level
            // (that doesn't happen in vanilla but could happen in mods like ShipLogSlideReelPlayer)
            // although it requires the parent entry to be returned by id by _shipLogManager
            // ShipLogSlideReelPlayer but I'll leave this just in case another mod adds more levels...
            if (!entry.HasRevealedParent())
            {
                return 0;
            }
            return 1 + GetEntryIndentation(_shipLogManager.GetEntry(entry.GetParentID()));
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

        private bool IsEntryMarkedOnHUD(ShipLogEntry entry)
        {
            return entry.GetID().Equals(_entryHUDMarker.GetMarkedEntryID());
        }

        private bool CanEntryBeMarkedOnHUD(ShipLogEntry entry)
        {
            return entry.GetState() == ShipLogEntry.State.Explored && Locator.GetEntryLocation(entry.GetID()) != null;
        }

        private void SetupPrompts()
        {
            // TODO: Translations
            _openPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.OpenSuitLog), "Open Suit Log");
            _closePrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.CloseSuitLog), "Close Suit Log");
            _viewEntriesPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.ViewEntries), UITextLibrary.GetString(UITextType.LogViewEntriesPrompt));
            _closeEntriesPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.CloseEntries), "Close Entries");
            _markOnHUDPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.MarkEntryOnHUD), ""); // This is updated
            Locator.GetPromptManager().AddScreenPrompt(_openPrompt, PromptPosition.UpperRight);
            Locator.GetPromptManager().AddScreenPrompt(_closePrompt, PromptPosition.UpperRight);
            Locator.GetPromptManager().AddScreenPrompt(_viewEntriesPrompt, PromptPosition.UpperRight);
            Locator.GetPromptManager().AddScreenPrompt(_closeEntriesPrompt, PromptPosition.UpperRight);
            Locator.GetPromptManager().AddScreenPrompt(_markOnHUDPrompt, PromptPosition.UpperRight);
            _descField.SetupPrompts();
        }

        private void UpdatePromptsVisibility()
        {
            _openPrompt.SetVisibility(IsSuitLogOpenable());
            _closePrompt.SetVisibility(_open && !_isEntryMenuOpen);
            _viewEntriesPrompt.SetVisibility(_open && !_isEntryMenuOpen);
            _closeEntriesPrompt.SetVisibility(_open && _isEntryMenuOpen);
            bool showMarkOnHUDPrompt = false;
            if (_open && _isEntryMenuOpen)
            {
                ShipLogEntry entry = _entryItems[_selectedItem];
                if (CanEntryBeMarkedOnHUD(entry))
                {
                    showMarkOnHUDPrompt = true;
                    string text = IsEntryMarkedOnHUD(entry) ? 
                        UITextLibrary.GetString(UITextType.LogRemoveMarkerPrompt) : 
                        UITextLibrary.GetString(UITextType.LogMarkLocationPrompt);
                    _markOnHUDPrompt.SetText(text);
                }
            }
            _markOnHUDPrompt.SetVisibility(showMarkOnHUDPrompt);
            _descField.UpdatePromptsVisibility();
        }
 
        private void HideAllPrompts()
        {
            _openPrompt.SetVisibility(false);
            _closePrompt.SetVisibility(false);
            _viewEntriesPrompt.SetVisibility(false);
            _closeEntriesPrompt.SetVisibility(false);
            _markOnHUDPrompt.SetVisibility(false);
            _descField.HideAllPrompts();
        }

        private void UpdateListUI()
        {
            // Same scrolling behaviour as ship log map mode
            int firstItem = _selectedItem <= 4 ? 0 : _selectedItem - 4;
            for (int i = 0; i < ListSize; i++)
            {
                Text text = _textList[i];
                int itemIndex = firstItem + i;
                if (itemIndex < _items.Count)
                {
                    ListItem item = _items[itemIndex];
                    string displayText = item.text + " "; // Space for the "icons"
                    displayText = new string(' ', item.indentation) + displayText;
                    if (item.markedOnHUD)
                    {
                        displayText += "<color=#9DFCA9>[V]</color>";
                    }                    
                    if (item.unread)
                    {
                        // In ship log, astro objects use the * symbol with the unread color, but this is better...
                        displayText += "<color=#9DFCA9>[!]</color>";
                    }
                    if (item.moreToExplore)
                    {
                        displayText += "<color=#EE8E3B>[*]</color>";
                    }
                    if (itemIndex != _selectedItem)
                    {
                        // Transparent non-selected items
                        text.canvasRenderer.SetAlpha(0.5f);
                        // Space for the select arrow icon
                        displayText = "  " + displayText;
                    }
                    else
                    {
                        text.canvasRenderer.SetAlpha(1f);
                        // Select arrow icon
                        displayText = "<color=#FFA431>></color> " + displayText;
                    }
                    text.color = item.rumored ? Locator.GetUIStyleManager().GetShipLogRumorColor() : Color.white;
                    text.text = displayText;
                    text.gameObject.SetActive(true);
                }
                else
                {
                    text.gameObject.SetActive(false);
                }
            }
        }

        private void SetupUI()
        {
            GameObject canvas = GameObject.Find("PlayerHUD/HelmetOnUI/UICanvas/");
            _suitLog = new GameObject("SuitLog", typeof(RectTransform));
            SetParent(_suitLog.transform, canvas.transform);
            _suitLog.transform.localPosition = new Vector3(-280, 260, 0);

            // Title
            _titleText = CreateText();
            _titleText.gameObject.name = "Title";
            _titleText.gameObject.SetActive(true);
            _titleText.fontSize = 40;
            RectTransform titleRect = _titleText.GetComponent<RectTransform>();
            SetParent(titleRect, _suitLog.transform);
            titleRect.anchoredPosition = new Vector2(0, 55);
            titleRect.pivot = new Vector2(0, 1);

            _textList = new Text[ListSize];
            for (int i = 0; i < ListSize; i++)
            {
                Text text = CreateText();
                RectTransform textTransform = text.GetComponent<RectTransform>();
                SetParent(textTransform, _suitLog.transform);
                textTransform.anchoredPosition = new Vector2(0, -i * 35);
                textTransform.pivot = new Vector2(0, 1);
                _textList[i] = text;
            }
            
            _suitLogAnimator = _suitLog.AddComponent<CanvasGroupAnimator>();
            _suitLogAnimator.SetImmediate(0f, Vector3.one); // Start closed (_open = closed)

            _notificationsAnimator = canvas.transform.Find("Notifications/Mask/LayoutGroup").gameObject.AddComponent<CanvasGroupAnimator>();
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
            if (_instance._open && mode != InputMode.Menu)
            {
                // This is done because the input mode on vision, projection, remote conversation, death, memory uplink...
                // And in some cases then the previous input mode is restored an in others it changes to Character
                // But don't close it if pausing game
                // (going to Menu is also possible in preflight list but Suit Log should be closed when doing that)
                _instance.CloseSuitLog();
            }
        }
    }
}
