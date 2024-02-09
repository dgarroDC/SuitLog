using System;
using System.Collections.Generic;
using OWML.Common;
using UnityEngine;

namespace SuitLog;

public class SuitLogMode : ShipLogMode
{
    public SuitLogItemList itemList;
    public ShipLogMapMode shipLogMap;

    private ScreenPromptList _upperRightPromptList;
    private OWAudioSource _oneShotSource;
    private ShipLogEntryHUDMarker _entryHUDMarker;
    private ShipLogManager _shipLogManager;

    private ScreenPrompt _viewEntriesPrompt;
    private ScreenPrompt _closeEntriesPrompt;
    private ScreenPrompt _markOnHUDPrompt;

    private Dictionary<string, ShipLogAstroObject> _shipLogAstroObjects;
    private List<string> _displayedAtroObjectIds = new();
    private List<ShipLogEntry> _displayedEntryItems = new();

    private bool _isEntryMenuOpen;
    private string _selectedAstroObjectID; // TODO: Redundant?

    public override void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
    {
        _upperRightPromptList = upperRightPromptList;
        _oneShotSource = oneShotSource;

        _shipLogManager = Locator.GetShipLogManager();
        _entryHUDMarker = FindObjectOfType<ShipLogEntryHUDMarker>();

        _shipLogAstroObjects = new Dictionary<string, ShipLogAstroObject>();
        foreach (ShipLogAstroObject[] astroObjects in shipLogMap._astroObjects)
        {
            foreach (ShipLogAstroObject astroObject in astroObjects)
            {
                // We want to use the ShipLogAstroObject to use the GetName patched by New Horizons...
                _shipLogAstroObjects.Add(astroObject.GetID(), astroObject);
            }
        }
        foreach (ShipLogEntry entry in _shipLogManager.GetEntryList())
        {
            string astroObjectID = entry.GetAstroObjectID();
            if (!_shipLogAstroObjects.ContainsKey(astroObjectID))
            {
                SuitLog.Instance.ModHelper.Console.WriteLine(
                    $"Entry {entry.GetID()} has an invalid astro object id {entry.GetAstroObjectID()}, " +
                    $"this may be an error in a New Horizons addon, please report this error!\n" +
                    $"The entry won't be shown in the Suit Log", MessageType.Error);
            }
        }

        // Don't use the one of the map mode because with New Horizons it could be an astro object not present
        // in the Suit Log (in vanilla there is Timber Hearth that is always there), just select the first item...
        _selectedAstroObjectID = null; 
        _isEntryMenuOpen = false;

        SetupPrompts();
    }

    private void SetupPrompts()
    {
        _viewEntriesPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.ViewEntries), UITextLibrary.GetString(UITextType.LogViewEntriesPrompt));
        // TODO: Close Entries is not the best text for hidden astro object
        _closeEntriesPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.CloseEntries), "Close Entries");
        _markOnHUDPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.MarkEntryOnHUD), ""); // This is updated
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        LoadAstroObjectsMenu();
        
        Locator.GetPromptManager().AddScreenPrompt(_viewEntriesPrompt, _upperRightPromptList, TextAnchor.MiddleRight);
        Locator.GetPromptManager().AddScreenPrompt(_closeEntriesPrompt, _upperRightPromptList, TextAnchor.MiddleRight);
        Locator.GetPromptManager().AddScreenPrompt(_markOnHUDPrompt, _upperRightPromptList, TextAnchor.MiddleRight);
    }
    
    private void LoadAstroObjectsMenu()
    {
        itemList.selectedIndex = 0; // Unnecessary statement probably...
        List<Tuple<string, bool, bool, bool>> items = new();
        _displayedAtroObjectIds.Clear();
        foreach ((string astroObjectId, ShipLogAstroObject astroObject) in _shipLogAstroObjects)
        {
            astroObject.OnEnterComputer();
            astroObject.UpdateState();
            if (astroObject.IsVisible()) // Exploded, Rumored and Hidden with !_invisibleWhenHidden
            {
                if (_selectedAstroObjectID == null)
                {
                    // This will make the first item to be selected the first time
                    _selectedAstroObjectID = astroObjectId;
                }

                if (astroObjectId == _selectedAstroObjectID)
                {
                    itemList.selectedIndex = items.Count; // Next element to insert
                }

                items.Add(new Tuple<string, bool, bool, bool>(
                    GetColoredName(astroObject.GetName(), astroObject.GetState()),
                    false,
                    astroObject._unviewedObj.activeSelf, // In ship log, astro objects use the * symbol with the unread color, but this is better...
                    false
                ));
                _displayedAtroObjectIds.Add(astroObjectId);
            }
        }

        itemList.contentsItems = items; // TODO: API
        itemList.SetName("Suit Log");
    }

    private void LoadEntriesMenu()
    {
        List<Tuple<string, bool, bool, bool>> items = new();
        _displayedEntryItems.Clear();
        ShipLogAstroObject selectedAstroObject = _shipLogAstroObjects[_selectedAstroObjectID];
        // Just in case the log was updated...
        selectedAstroObject.OnEnterComputer();
        // Don't use GetEntries, patched by ShipLogSlideReelPlayer TODO: not anymore?
        List<ShipLogEntry> entries = selectedAstroObject._entries;
        foreach (ShipLogEntry entry in entries)
        {
            ShipLogEntry.State state = entry.GetState();
            if (state == ShipLogEntry.State.Explored || state == ShipLogEntry.State.Rumored)
            {
                items.Add(new Tuple<string, bool, bool, bool>(
                    GetNameWithIndentation(GetColoredName(entry.GetName(false), state), GetEntryIndentation(entry)),
                    IsEntryMarkedOnHUD(entry),
                    entry.HasUnreadFacts(),
                    entry.HasMoreToExplore()
                ));
                // TODO: Move to another method: Load + Display!
                _displayedEntryItems.Add(entry);
            }
        }

        itemList.contentsItems = items; // TODO: API
        itemList.SetName(selectedAstroObject.GetName());
    }

    private string GetColoredName(string name, ShipLogEntry.State state)
    {
        Color? color = null;
        if (state == ShipLogEntry.State.Rumored)
        {
            color = Locator.GetUIStyleManager().GetShipLogRumorColor();
        }
        else if (state == ShipLogEntry.State.Hidden)
        {
            color = Color.red;
        }

        if (color.HasValue)
        {
            name = "<color=#" + ColorUtility.ToHtmlStringRGB(color.Value) + ">" + name + "</color>";
        }

        return name;
    }
    
    private string GetNameWithIndentation(string name, int indentation)
    {
        return new string(' ', indentation) + name;
    }
    
    private int GetEntryIndentation(ShipLogEntry entry)
    {
        // This work even for more than one indentation level
        // (that doesn't happen in vanilla but could happen in mods like ShipLogSlideReelPlayer)
        // although it requires the parent entry to be returned by id by _shipLogManager
        // ShipLogSlideReelPlayer doesn't need it anymore but I'll leave this just in case
        // another mod adds more levels...
        if (!entry.HasRevealedParent())
        {
            return 0;
        }
        return 1 + GetEntryIndentation(_shipLogManager.GetEntry(entry.GetParentID()));
    }
    
    private bool IsEntryMarkedOnHUD(ShipLogEntry entry)
    {
        return entry.GetID().Equals(_entryHUDMarker.GetMarkedEntryID());
    }

    public override void ExitMode()
    {
        if (_isEntryMenuOpen)
        {
            CloseEntryMenu();
        }
        
        Locator.GetPromptManager().RemoveScreenPrompt(_viewEntriesPrompt);
        Locator.GetPromptManager().RemoveScreenPrompt(_closeEntriesPrompt);
        Locator.GetPromptManager().RemoveScreenPrompt(_markOnHUDPrompt);
    }

    public override void OnEnterComputer()
    {
        // No-op
    }

    public override void OnExitComputer()
    {
        // No-op
    }

    public override void UpdateMode()
    {
        int prevSelectedIndex = itemList.selectedIndex;
        int selectionChange = itemList.UpdateList();
        if (selectionChange != 0)
        {
            if (!_isEntryMenuOpen)
            {
                _selectedAstroObjectID = _displayedAtroObjectIds[itemList.selectedIndex];
            }
            else
            {
                MarkAsRead(prevSelectedIndex);
                UpdateSelectedEntry();
            }
        } 
        else if (_isEntryMenuOpen && _displayedEntryItems.Count > 0 && Input.IsNewlyPressed(Input.Action.MarkEntryOnHUD))
        {
            ShipLogEntry entry = _displayedEntryItems[prevSelectedIndex];
            if (CanEntryBeMarkedOnHUD(entry))
            {
                if (IsEntryMarkedOnHUD(entry))
                {
                    _entryHUDMarker.SetEntryLocation(null);
                    _oneShotSource.PlayOneShot(AudioType.ShipLogUnmarkLocation);
                }
                else
                {
                    _entryHUDMarker.SetEntryLocation(Locator.GetEntryLocation(entry.GetID()));
                    _oneShotSource.PlayOneShot(AudioType.ShipLogMarkLocation);
                }
                LoadEntriesMenu();
            }
        }
        else if (!_isEntryMenuOpen && _displayedAtroObjectIds.Count > 0 && Input.IsNewlyPressed(Input.Action.ViewEntries))
        {
            OpenEntryMenu();
        }
        else if (_isEntryMenuOpen && Input.IsNewlyPressed(Input.Action.CloseEntries))
        {
            CloseEntryMenu();
        }

        UpdatePromptsVisibility();
    }

    private void UpdatePromptsVisibility()
    {
        // > 0 because it could be empty
        _viewEntriesPrompt.SetVisibility(!_isEntryMenuOpen && _displayedAtroObjectIds.Count > 0 && _shipLogAstroObjects[_selectedAstroObjectID].GetState() != ShipLogEntry.State.Hidden);
        _closeEntriesPrompt.SetVisibility(_isEntryMenuOpen);
        bool showMarkOnHUDPrompt = false;
        if (_isEntryMenuOpen && _displayedEntryItems.Count > 0)
        {
            ShipLogEntry entry = _displayedEntryItems[itemList.selectedIndex];
            if (CanEntryBeMarkedOnHUD(entry))
            {
                showMarkOnHUDPrompt = true;
                string text = IsEntryMarkedOnHUD(entry) ? // TODO: Use the index and bool on tuple?
                    UITextLibrary.GetString(UITextType.LogRemoveMarkerPrompt) : 
                    UITextLibrary.GetString(UITextType.LogMarkLocationPrompt);
                _markOnHUDPrompt.SetText(text);
            }
        }
        _markOnHUDPrompt.SetVisibility(showMarkOnHUDPrompt);
    }

    public void HideAllPrompts()
    {
        _viewEntriesPrompt.SetVisibility(false);
        _closeEntriesPrompt.SetVisibility(false);
        _markOnHUDPrompt.SetVisibility(false);
    }

    private void OpenEntryMenu()
    {
        LoadEntriesMenu();
        itemList.selectedIndex = 0;
        itemList.DescriptionFieldOpen();
        UpdateSelectedEntry();
        _isEntryMenuOpen = true;
        _oneShotSource.PlayOneShot(AudioType.ShipLogSelectPlanet);
    }

    private void CloseEntryMenu()
    {
        if (_displayedEntryItems.Count > 0)
        {
            MarkAsRead(itemList.selectedIndex);
        }
        itemList.DescriptionFieldClose();
        LoadAstroObjectsMenu();
        HidePhoto();
        HideQuestionMark();
        _displayedEntryItems.Clear(); // TODO: Why?
        _isEntryMenuOpen = false;
        _oneShotSource.PlayOneShot(AudioType.ShipLogDeselectPlanet);
    }

    private void MarkAsRead(int index)
    {
        // TODO: setting to disable mark on read
        // TODO: Test this changed in all scenarios
        Tuple<string,bool,bool,bool> item = itemList.contentsItems[index];
        if (item.Item3)
        {
            ShipLogEntry entry = _displayedEntryItems[index];
            entry.MarkAsRead();
            LoadEntriesMenu(); // TODO: Maybe move this out, not necessary on close entry menu...
        }
    }

    private bool CanEntryBeMarkedOnHUD(ShipLogEntry entry)
    {
        return entry.GetState() == ShipLogEntry.State.Explored && Locator.GetEntryLocation(entry.GetID()) != null;
    }

    private void UpdateSelectedEntry()
    {
        itemList.DescriptionFieldClear();
        if (_displayedEntryItems.Count > 0)
        {
            ShipLogEntry entry = _displayedEntryItems[itemList.selectedIndex];
            List<ShipLogFact> facts = entry.GetFactsForDisplay();
            foreach (ShipLogFact fact in facts)
            {
                ShipLogFactListItem item = itemList.DescriptionFieldGetNextItem();
                item.DisplayFact(fact);
                item.StartTextReveal();
            }

            if (entry.HasMoreToExplore())
            {
                itemList.DescriptionFieldGetNextItem()
                    .DisplayText(UITextLibrary.GetString(UITextType.ShipLogMoreThere));
            }
            if (entry.GetState() == ShipLogEntry.State.Explored)
            {
                ShowPhoto(entry);
            }
            else
            {
                ShowQuestionMark();
            }
        }
        else
        {
            // In MapModeMode, this would happen at OpenEntryMenu
            itemList.DescriptionFieldGetNextItem()
                .DisplayText(UITextLibrary.GetString(UITextType.LogNoDiscoveriesPrompt));
            ShowQuestionMark();
        }
    }
    
    private void ShowPhoto(ShipLogEntry entry)
    {
        HideQuestionMark();
        itemList.photo.gameObject.SetActive(true);
        itemList.photo.sprite = entry.GetSprite();
    }

    private void HidePhoto()
    {
        itemList.photo.gameObject.SetActive(false);
        itemList.photo.sprite = null;
    }

    private void ShowQuestionMark()
    {
        HidePhoto();
        itemList.questionMark.gameObject.SetActive(true);
    }

    private void HideQuestionMark()
    {
        itemList.questionMark.gameObject.SetActive(false);
    }

    public override bool AllowModeSwap()
    {
        return true;
    }

    public override bool AllowCancelInput()
    {
        return !_isEntryMenuOpen;
    }

    public override string GetFocusedEntryID()
    {
        return "";
    }
}
