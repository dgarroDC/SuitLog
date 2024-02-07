using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SuitLog;

// A lot of code is copied from Custom Ship Log Modes (CSLM) ShipLogItemList
public class SuitLogItemList : MonoBehaviour
{
    private static GameObject _prefab;
    private static Transform _commonParent;
    private static DescriptionField _descriptionField; // TODO: idk

    private const int TotalUIItems = 10; // Always 10, not variable like in Custom Ship Log Modes

    public OWAudioSource oneShotSource; // Well this is used for the custom modes I guess
    public Text nameField;
    public Image photo;
    public DescriptionField descriptionField;

    public int selectedIndex;
    public List<Text> uiItems; // TODO: ShipLogEntryListItem??
    public List<Tuple<string, bool, bool, bool>> contentsItems = new();
    public ListNavigator listNavigator;

    public RectTransform upperRightPromptsRect;

    public CanvasGroupAnimator suitLogAnimator;
    public CanvasGroupAnimator notificationsAnimator;

    public static void CreatePrefab(ScreenPromptList upperRightPromptList)
    {
        GameObject canvas = GameObject.Find("PlayerHUD/HelmetOnUI/UICanvas/");
        
        // Parent object for all item lists
        GameObject commonParentGo = new GameObject("SuitLog", typeof(RectTransform));
        _commonParent = commonParentGo.transform;
        SuitLog.SetParent(_commonParent, canvas.transform);
        _commonParent.localPosition = new Vector3(-280, 260, 0);

        GameObject prefab = new GameObject("ItemsList", typeof(RectTransform));
        SuitLogItemList itemList = prefab.AddComponent<SuitLogItemList>();
        itemList.Setup(canvas, upperRightPromptList);

        _descriptionField = new DescriptionField(_commonParent, upperRightPromptList);

        _prefab = prefab; // We can do it in the same frame for now, unlike CSLM
    }

    private void Setup(GameObject canvas, ScreenPromptList upperRightPromptList)
    {
        // TODO: Make this part of list
        Transform probeDisplay = canvas.transform.Find("HUDProbeDisplay");
        GameObject photoRoot = Instantiate(probeDisplay.gameObject, transform);
        photoRoot.transform.localPosition = probeDisplay.transform.localPosition - _commonParent.localPosition; // Offset to match the original
        photoRoot.DestroyAllComponents<ProbeLauncherUI>();
        photoRoot.SetActive(true);
        photo = photoRoot.GetComponentInChildren<Image>();

        // Title
        nameField = SuitLog.CreateText();
        nameField.gameObject.name = "Title";
        nameField.gameObject.SetActive(true);
        nameField.fontSize = 40;
        RectTransform titleRect = nameField.GetComponent<RectTransform>();
        SuitLog.SetParent(titleRect, transform);
        titleRect.anchoredPosition = new Vector2(0, 55);
        titleRect.pivot = new Vector2(0, 1);

        uiItems = new List<Text>();
        for (int i = 0; i < TotalUIItems; i++)
        {
            Text text = SuitLog.CreateText();
            RectTransform textTransform = text.GetComponent<RectTransform>(); 
            SuitLog.SetParent(textTransform, transform);
            textTransform.anchoredPosition = new Vector2(0, -i * 35);
            textTransform.pivot = new Vector2(0, 1);
            uiItems.Add(text);
        }
            
        suitLogAnimator = gameObject.AddComponent<CanvasGroupAnimator>();
        suitLogAnimator.SetImmediate(0f, Vector3.one); // Start closed (_open = closed)

        notificationsAnimator = canvas.transform.Find("Notifications/Mask/LayoutGroup").gameObject.AddComponent<CanvasGroupAnimator>();
        
        GameObject notificationAudio = GameObject.Find("Player_Body/Audio_Player/NotificationAudio");
        GameObject audioSourceObject = Instantiate(notificationAudio);
        SuitLog.SetParent(audioSourceObject.transform, transform);
        oneShotSource = audioSourceObject.GetComponent<OWAudioSource>();
        listNavigator = gameObject.AddComponent<ListNavigator>();
        upperRightPromptsRect = upperRightPromptList.GetComponent<RectTransform>();
    }

    public static void Make(Action<MonoBehaviour> callback)
    {
        SuitLog.Instance.ModHelper.Events.Unity.RunWhen(() => _prefab != null, () =>
        {
            GameObject itemListModeGo = Instantiate(_prefab);
            SuitLog.SetParent(itemListModeGo.transform, _commonParent);
            SuitLogItemList itemList = itemListModeGo.GetComponent<SuitLogItemList>();
            itemList.descriptionField = _descriptionField; // TODO: idk
            callback.Invoke(itemList);
        });
    }

    public void Open()
    {
        suitLogAnimator.AnimateTo(1, Vector3.one, SuitLog.OpenAnimationDuration);
        // Make notifications slightly transparent to avoid unreadable overlapping
        notificationsAnimator.AnimateTo(0.35f, Vector3.one, SuitLog.OpenAnimationDuration);
        oneShotSource.PlayOneShot(AudioType.ShipLogSelectPlanet);
    }

    public void Close()
    {
        suitLogAnimator.AnimateTo(0, Vector3.one, SuitLog.CloseAnimationDuration);
        notificationsAnimator.AnimateTo(1, Vector3.one, SuitLog.CloseAnimationDuration);
        oneShotSource.PlayOneShot(AudioType.ShipLogDeselectPlanet);
    }
    
    public void HideAllPrompts()
    {
        descriptionField.HideAllPrompts();
    }

    private void SetPromptsPosition(float positionY)
    {
        // See PromptManager: Lower the prompts when the image is displayed
        Vector2 anchoredPosition = upperRightPromptsRect.anchoredPosition;
        anchoredPosition.y = positionY;
        upperRightPromptsRect.anchoredPosition = anchoredPosition;
    }

    public void UpdateListUI()
    { 
        // Same scrolling behaviour as ship log map mode
        int firstItem = selectedIndex <= 4 ? 0 : selectedIndex - 4;
        for (int i = 0; i < TotalUIItems; i++)
        {
            Text text = uiItems[i];
            int itemIndex = firstItem + i;
            if (itemIndex < contentsItems.Count)
            {
                Tuple<string, bool, bool, bool> item = contentsItems[itemIndex];
                string displayText = item.Item1 + " "; // Space for the "icons"
                if (item.Item2)
                {
                    displayText += "<color=#9DFCA9>[V]</color>";
                }                    
                if (item.Item3)
                {
                    displayText += "<color=#9DFCA9>[!]</color>";
                }
                if (item.Item4)
                {
                    displayText += "<color=#EE8E3B>[*]</color>";
                }
                if (itemIndex != selectedIndex)
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
                text.text = displayText;
                text.gameObject.SetActive(true);
            }
            else
            {
                text.gameObject.SetActive(false);
            }
        }
        
        // TODO: Clarify this in docs
        if (photo.enabled)
        {
            SetPromptsPosition(-250f);
        }
        else
        {
            SetPromptsPosition(0f);
        }
    }

    public int UpdateList()
    {
        int selectionChange  = 0;

        if (contentsItems.Count >= 2)
        {
            selectionChange = listNavigator.GetSelectionChange();
            if (selectionChange != 0)
            {
                selectedIndex += selectionChange;
                if (selectedIndex == -1)
                {
                    selectedIndex = contentsItems.Count - 1;
                }
                else if (selectedIndex == contentsItems.Count)
                {
                    selectedIndex = 0;
                }
                oneShotSource.PlayOneShot(AudioType.ShipLogMoveBetweenEntries);
            }
        }

        UpdateListUI();

        return selectionChange;
    }

    public void SetName(string name)
    {
        nameField.text = name;
    }

    public void DescriptionFieldClear()
    {
        descriptionField.Clear();
    }

    public ShipLogFactListItem DescriptionFieldGetNextItem()
    {
        return descriptionField.GetNextItem();
    }

    public int GetIndexUI(int index)
    {
        // This is copied from UpdateListUI()... (Well, from CSLM)
        int lastSelectable = 4;
        int itemIndexOnTop = selectedIndex <= lastSelectable ? 0 : selectedIndex - lastSelectable;

        int uiIndex = index - itemIndexOnTop;
        return uiIndex < TotalUIItems ? uiIndex : -1;
    }
    
    // TODO: Desc field -> open, close, update?
}
