using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;

namespace SuitLog
{
    public class DescriptionField
    {
        private OWAudioSource _audioSource;
        private RectTransform _descField;
        private RectTransform _factList;
        private ShipLogFactListItem[] _items;
        private float _origYPos;
        private int _displayCount = 0;
        private bool _visible;
        private CanvasGroupAnimator _animator;
        private Vector3 _closeScale;
        
        private ScreenPrompt _scrollPromptGamepad;
        private ScreenPrompt _scrollPromptKbm;

        internal DescriptionField(GameObject suitLog)
        {
            // I should probably reuse ShipLogEntryDescriptionField...
            GameObject descFieldObject = new GameObject("DescriptionField");
            _descField = descFieldObject.AddComponent<RectTransform>();
            SuitLog.SetParent(_descField, suitLog.transform);
            _descField.sizeDelta = new Vector2(600, 290);
            _descField.anchoredPosition = new Vector2(0, -425); 
            _descField.anchorMin = new Vector2(0, 1);
            _descField.anchorMax = new Vector2(1, 1);
            _descField.pivot = new Vector2(0, 1);
            descFieldObject.AddComponent<RectMask2D>();
            descFieldObject.AddComponent<Image>().color = new Color(0.5468f, 0.816f, 0.9057f, 0.18f);

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
            
            _items = new ShipLogFactListItem[10];
            for (int i = 0; i < _items.Length; i++)
            {
                Text text = SuitLog.CreateText();
                SuitLog.SetParent(text.transform, _factList);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
                ShipLogFactListItem item = text.gameObject.AddComponent<ShipLogFactListItem>();
                item._text = text;
                // Set active to avoid NRE on first UpdatePromptsVisibility?
                item.gameObject.SetActive(true);
                _items[i] = item;
            }

            _animator = descFieldObject.AddComponent<CanvasGroupAnimator>();
            _closeScale = new Vector3(1f, 0f, 1f);
            _animator.SetImmediate(0f, new Vector3(1f, 0f, 1f)); // Start closed
            _visible = false;

            GameObject revealAudio = GameObject.Find("Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/DescriptionField/TextRevealAudioSource");
            GameObject audioSourceObject = Object.Instantiate(revealAudio);
            SuitLog.SetParent(audioSourceObject.transform, descFieldObject.transform);
            _audioSource = audioSourceObject.GetComponent<OWAudioSource>();
            _audioSource.SetTrack(OWAudioMixer.TrackName.Player); // Not sure if we need this
        }

        public void Update()
        {
            UpdateTextReveal();
            UpdateScroll();
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
                if (item._fact != null)
                {
                    item._text.text = "- " + item._text.text;
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
            if (!_visible) return; // This case shouldn't be possible because of _isEntryMenuOpen = false
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
            Locator.GetPromptManager().AddScreenPrompt(_scrollPromptGamepad, PromptPosition.UpperRight);
            Locator.GetPromptManager().AddScreenPrompt(_scrollPromptKbm, PromptPosition.UpperRight);
            // _scrollPromptGamepad.SetDisplayState(ScreenPrompt.DisplayState.Attention);
            // _scrollPromptKbm.SetDisplayState(ScreenPrompt.DisplayState.Attention);
        }

        public void UpdatePromptsVisibility()
        {
            bool usingGamepad = OWInput.UsingGamepad();
            bool scrollable = GetMaxScroll() < 0f; // Is this ok? 
            _scrollPromptGamepad.SetVisibility(_visible && scrollable && usingGamepad);
            _scrollPromptKbm.SetVisibility(_visible && scrollable && !usingGamepad);
        }

        public void HideAllPrompts()
        {
            _scrollPromptGamepad.SetVisibility(false);
            _scrollPromptKbm.SetVisibility(false);
        }
 
        public void SetEntry(ShipLogEntry entry)
        {
            ResetListPos();
            List<ShipLogFact> facts = entry.GetFactsForDisplay();
            for (int i = 0; i < _items.Length; i++)
            {
                if (i < facts.Count)
                {
                    _items[i].DisplayFact(facts[i]);
                    _items[i].StartTextReveal();
                }
                else
                {
                    _items[i].Clear();
                }
            }

            _displayCount = facts.Count;

            if (entry.HasMoreToExplore())
            {
                _items[_displayCount].DisplayText("- " + UITextLibrary.GetString(UITextType.ShipLogMoreThere));
                _displayCount++;
            }
        }

        public void Open()
        {
            _visible = true;
            _animator.AnimateTo( 1,  Vector3.one , SuitLog.OpenAnimationDuration);
        }

        public void Close()
        {
            _visible = false;
            _animator.AnimateTo( 0, _closeScale, SuitLog.CloseAnimationDuration);
            if (_audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
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
