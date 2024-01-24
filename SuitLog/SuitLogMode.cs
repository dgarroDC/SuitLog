using System;
using System.Collections.Generic;
using OWML.Common;

namespace SuitLog;

public class SuitLogMode : ShipLogMode
{
    public SuitLogItemList itemList;
    public ShipLogMapMode shipLogMap;

    private ScreenPromptList _upperRightPromptList;
    private OWAudioSource _oneShotSource;
    private ShipLogEntryHUDMarker _entryHUDMarker;
    private ShipLogManager _shipLogManager;

    private Dictionary<string, ShipLogAstroObject> _shipLogAstroObjects;
    private HashSet<string> _astroObjectIds;
    private List<string> _displayedAtroObjectIds = new();
    private List<ShipLogEntry> _displayedEntryItems = new();

    private bool _isEntryMenuOpen; // <=> _entryItems not empty TODO: what? maybe this will change
    private string _selectedAstroObjectID;

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
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        LoadAstroObjectsMenu();
    }
    
    private void LoadAstroObjectsMenu()
    {
        itemList.selectedIndex = 0; // Unnecessary statement probably...
        List<Tuple<string, bool, bool, bool>> items = new();
        _displayedAtroObjectIds.Clear();
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
                itemList.selectedIndex = items.Count; // Next element to insert
            }
            //FIXME: state == ShipLogEntry.State.Rumored
            items.Add(new Tuple<string, bool, bool, bool>(
                astroObject.GetName(), 
                false,
                astroObject._unviewedObj.activeSelf, // In ship log, astro objects use the * symbol with the unread color, but this is better...
                false
            ));
            _displayedAtroObjectIds.Add(astroObjectId);
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
            if (entry.GetState() == ShipLogEntry.State.Explored || entry.GetState() == ShipLogEntry.State.Rumored)
            {
                //FIXME: entry.GetState() == ShipLogEntry.State.Rumored 
                items.Add(new Tuple<string, bool, bool, bool>(
                    GetEntryNameWithIndentation(entry),
                    IsEntryMarkedOnHUD(entry),
                    entry.HasUnreadFacts(),
                    entry.HasMoreToExplore()
                ));
                _displayedEntryItems.Add(entry);
            }
        }

        itemList.contentsItems = items; // TODO: API
        itemList.SetName(selectedAstroObject.GetName());
    }

    private string GetEntryNameWithIndentation(ShipLogEntry entry)
    {
        string name = entry.GetName(false);
        int indentation = GetEntryIndentation(entry);
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
        else if (_isEntryMenuOpen && Input.IsNewlyPressed(Input.Action.MarkEntryOnHUD))
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
        else if (!_isEntryMenuOpen && Input.IsNewlyPressed(Input.Action.ViewEntries))
        {
            OpenEntryMenu();
        }
        else if (_isEntryMenuOpen && Input.IsNewlyPressed(Input.Action.CloseEntries))
        {
            CloseEntryMenu();
        }

        if (_isEntryMenuOpen)
        {
            itemList.descriptionField.Update();
        }
    }
    
    private void OpenEntryMenu()
    {
        LoadEntriesMenu();
        itemList.selectedIndex = 0;
        itemList.descriptionField.Open();
        UpdateSelectedEntry();
        _isEntryMenuOpen = true;
        _oneShotSource.PlayOneShot(AudioType.ShipLogSelectPlanet);
    }

    private void CloseEntryMenu()
    {
        MarkAsRead(itemList.selectedIndex);
        LoadAstroObjectsMenu();
        itemList.descriptionField.Close();
        HidePhoto();
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
            LoadEntriesMenu();
        }
    }

    private bool CanEntryBeMarkedOnHUD(ShipLogEntry entry)
    {
        return entry.GetState() == ShipLogEntry.State.Explored && Locator.GetEntryLocation(entry.GetID()) != null;
    }

    private void UpdateSelectedEntry()
    {
        ShipLogEntry entry = _displayedEntryItems[itemList.selectedIndex];
        itemList.DescriptionFieldClear();
        List<ShipLogFact> facts = entry.GetFactsForDisplay();
        foreach (ShipLogFact fact in facts)
        {
            ShipLogFactListItem item = itemList.DescriptionFieldGetNextItem();
            item.DisplayFact(fact);
            item.StartTextReveal();
        }

        if (entry.HasMoreToExplore())
        {
            itemList.DescriptionFieldGetNextItem().DisplayText(UITextLibrary.GetString(UITextType.ShipLogMoreThere));
        }
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
        itemList.photo.enabled = true;
        itemList.photo.sprite = entry.GetSprite();
        //FIXME: SetPromptsPosition(-250f);
    }

    private void HidePhoto()
    {
        itemList.photo.enabled = false;
        itemList.photo.sprite = null; // TODO: Is this needed?
        //FIXME: SetPromptsPosition(0f);
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
