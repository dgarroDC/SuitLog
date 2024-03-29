﻿using System;
using UnityEngine;
using UnityEngine.UI;

namespace SuitLog
{
    public class DescriptionField : MonoBehaviour
    {
        public OWAudioSource _audioSource;
        public RectTransform _descField;
        public RectTransform _factList;
        public ShipLogFactListItem[] _items;
        public float _origYPos;
        public int _displayCount = 0;
        public CanvasGroupAnimator _animator;
        public Vector3 _closeScale;
        
        public ScreenPromptList _upperRightPromptList;
        private ScreenPrompt _scrollPromptGamepad;
        private ScreenPrompt _scrollPromptKbm;

        public static DescriptionField Create(Transform parent, ScreenPromptList upperRightPromptList)
        {
            // I should probably reuse ShipLogEntryDescriptionField...
            GameObject descFieldObject = new GameObject("DescriptionField");
            DescriptionField descriptionField = descFieldObject.AddComponent<DescriptionField>();
            descriptionField.Setup(parent, upperRightPromptList);
            return descriptionField;
        }
        
        private void Setup(Transform parent, ScreenPromptList upperRightPromptList) {
            _descField = gameObject.AddComponent<RectTransform>();
            SuitLog.SetParent(_descField, parent);
            _descField.sizeDelta = new Vector2(600, 290);
            _descField.anchoredPosition = new Vector2(0, -425); 
            _descField.anchorMin = new Vector2(0, 1);
            _descField.anchorMax = new Vector2(1, 1);
            _descField.pivot = new Vector2(0, 1);
            gameObject.AddComponent<RectMask2D>();
            gameObject.AddComponent<Image>().color = new Color(0.5468f, 0.816f, 0.9057f, 0.18f);

            GameObject factListObject = new GameObject("FactList");
            _factList = factListObject.AddComponent<RectTransform>();
            SuitLog.SetParent(_factList, _descField);
            _factList.sizeDelta = new Vector2(0, 30000); // ?
            _factList.anchoredPosition = new Vector2(0, 0);
            _factList.anchorMin = new Vector2(0, 0);
            _factList.anchorMax = new Vector2(1, 1);
            _factList.pivot = new Vector2(0, 1);
            _origYPos = _factList.anchoredPosition.y;
            VerticalLayoutGroup verticalLayoutGroup = factListObject.AddComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.childForceExpandHeight = false;
            verticalLayoutGroup.childControlHeight = true;
            
            _items = new ShipLogFactListItem[10];
            for (int i = 0; i < _items.Length; i++)
            {
                SetupItem(i);
            }

            _animator = gameObject.AddComponent<CanvasGroupAnimator>();
            _closeScale = new Vector3(1f, 0f, 1f);
            _animator.SetImmediate(0f, _closeScale); // Start closed
            enabled = false;

            GameObject revealAudio = GameObject.Find("Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/DescriptionField/TextRevealAudioSource");
            GameObject audioSourceObject = UnityEngine.Object.Instantiate(revealAudio);
            SuitLog.SetParent(audioSourceObject.transform, gameObject.transform);
            _audioSource = audioSourceObject.GetComponent<OWAudioSource>();
            _audioSource.SetTrack(OWAudioMixer.TrackName.Player); // Not sure if we need this

            _upperRightPromptList = upperRightPromptList;
            
            Clear(); // Just to hide the default texts
        }

        private void SetupItem(int i)
        {
            GameObject itemContainer = new GameObject("ItemContainer_" + i);
            SuitLog.SetParent(itemContainer.transform, _factList.transform);
            itemContainer.AddComponent<RectTransform>().pivot = new Vector2(0, 1);
            HorizontalLayoutGroup horizontalLayoutGroup = itemContainer.AddComponent<HorizontalLayoutGroup>();
            // idk, this is mostly by trial and error like most layout stuff
            horizontalLayoutGroup.childForceExpandHeight = false;
            horizontalLayoutGroup.childControlHeight = true;
            horizontalLayoutGroup.childForceExpandWidth = false;
            horizontalLayoutGroup.childControlWidth = true;

            Text hyphen = SuitLog.CreateText();
            SuitLog.SetParent(hyphen.transform, itemContainer.transform);
            LayoutElement hyphenLayoutElement = hyphen.gameObject.AddComponent<LayoutElement>();
            hyphenLayoutElement.minWidth = 26;
            hyphenLayoutElement.preferredWidth = 26;
            hyphen.text = "- ";

            Text text = SuitLog.CreateText();
            SuitLog.SetParent(text.transform, itemContainer.transform);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            ShipLogFactListItem item = itemContainer.gameObject.AddComponent<ShipLogFactListItem>();
            item._text = text;
            _items[i] = item;

            // Patch 14 stuff, avoid NRE on Start()
            item._uiSizeSetter = item.gameObject.AddComponent<NoOpUiSizeSetter>();
            // Setting _requiresExternalInitialization is worthless because Awake is already called here, and _readyForResize=false won't work because MarkReadyForInitialization() on Start() of the item!
            // I could set all the values to the current ones (for any language and size), but easier to make the no-op class...
            // TODO: Maybe it would be nice to actually resize, even using the values of the Ship Log... Although it could be an issue of the hyphen/bullet being a separate text...

            // Set active to avoid NRE on first UpdatePromptsVisibility?
            itemContainer.SetActive(true);
            hyphen.gameObject.SetActive(true);
            text.gameObject.SetActive(true);
        }

        public void Update()
        {
            UpdateTextReveal(); // TODO: Clarify that this is done if fact assigned but prob not (only the main mode)
            UpdateScroll();
            UpdatePromptsVisibility();
        }

        private void UpdateTextReveal()
        {
            bool revealing = false;
            foreach (ShipLogFactListItem item in _items)
            {
                if (item.UpdateTextReveal())
                {
                    revealing = true;
                }
            }

            if (revealing && !_audioSource.isPlaying)
            {
                _audioSource.Play();
            }
            else if (!revealing && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        public void UpdateScroll()
        {
            bool usingGamepad = OWInput.UsingGamepad();
            float scroll;
            if (usingGamepad)
            {
                scroll = Input.GetValue(Input.Action.ScrollFactsGamepad) * Time.unscaledDeltaTime * 300f;
            }
            else
            {
                scroll = Input.GetValue(Input.Action.ScrollFactsKbm) * Time.unscaledDeltaTime * 300f;
            }
            Vector2 anchoredPosition = _factList.anchoredPosition;
            float targetScroll = anchoredPosition.y - scroll;
            float maxScroll = GetMaxScroll();
            anchoredPosition.y = Mathf.Clamp(targetScroll, _origYPos, _origYPos - maxScroll);
            _factList.anchoredPosition = anchoredPosition;
        }

        private float GetMaxScroll()
        {
            return Mathf.Min(0f,  GetListBottomPos() + _descField.rect.height);
        }

        public void SetupPrompts()
        {
            string prompt = UITextLibrary.GetString(UITextType.LogScrollTextPrompt);          
            _scrollPromptGamepad = new ScreenPrompt(Input.PromptCommands(Input.Action.ScrollFactsGamepad), prompt); 
            _scrollPromptKbm  = new ScreenPrompt(Input.PromptCommands(Input.Action.ScrollFactsKbm), prompt);
        }

        public void UpdatePromptsVisibility()
        {
            bool usingGamepad = OWInput.UsingGamepad();
            bool scrollable = GetMaxScroll() < 0f; // Is this ok? 
            _scrollPromptGamepad.SetVisibility( scrollable && usingGamepad);
            _scrollPromptKbm.SetVisibility(scrollable && !usingGamepad);
        }

        public void HideAllPrompts()
        {
            _scrollPromptGamepad.SetVisibility(false);
            _scrollPromptKbm.SetVisibility(false);
        }

        public void Open()
        {
            if (!enabled)
            {
                enabled = true;
                _animator.AnimateTo( 1,  Vector3.one , SuitLog.OpenAnimationDuration);
                Locator.GetPromptManager().AddScreenPrompt(_scrollPromptGamepad, _upperRightPromptList, TextAnchor.MiddleRight);
                Locator.GetPromptManager().AddScreenPrompt(_scrollPromptKbm, _upperRightPromptList, TextAnchor.MiddleRight);
                // _scrollPromptGamepad.SetDisplayState(ScreenPrompt.DisplayState.Attention);
                // _scrollPromptKbm.SetDisplayState(ScreenPrompt.DisplayState.Attention);
            }
        }

        public void Close()
        {
            if (enabled)
            {
                enabled = false;
                _animator.AnimateTo( 0, _closeScale, SuitLog.CloseAnimationDuration);
                if (_audioSource.isPlaying)
                {
                    _audioSource.Stop();
                }
                Locator.GetPromptManager().RemoveScreenPrompt(_scrollPromptGamepad);
                Locator.GetPromptManager().RemoveScreenPrompt(_scrollPromptKbm);
            }
        }

        public void Clear()
        {
            ResetListPos();
            _displayCount = 0;
            foreach (ShipLogFactListItem item in _items)
            {
                item.Clear();
            }
        }

        public ShipLogFactListItem GetNextItem()
        {
            int nextIndex = _displayCount;
            _displayCount++;
            if (nextIndex == _items.Length)
            {
                // Create a new item
                Array.Resize(ref _items, nextIndex + 1);
                SetupItem(nextIndex);
            }
            ShipLogFactListItem nextItem = _items[nextIndex];
            nextItem.DisplayText(string.Empty);
            return nextItem;
        }

        private void ResetListPos()
        {
            Vector2 anchoredPosition = _factList.anchoredPosition;
            anchoredPosition.y = 0;
            _factList.anchoredPosition = anchoredPosition;
        }

        private float GetListBottomPos()
        {
            if (_displayCount <= 0) return 0f; // Is this case possible? Maybe because of other mods
            ShipLogFactListItem lastItem = _items[_displayCount - 1];
            return lastItem.GetPosition().y - lastItem.GetHeight();
        }
    }
}
