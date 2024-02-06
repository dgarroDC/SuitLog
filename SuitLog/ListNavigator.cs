using UnityEngine;

namespace SuitLog
{
    public class ListNavigator : MonoBehaviour
    {
        private float _pressedUpTimer;
        private float _pressedDownTimer;
        private float _nextHoldUpTime;
        private float _nextHoldDownTime;

        public int GetSelectionChange()
        {
            // See ShipLogMapMode.UpdateMode()
            if (Input.IsPressed(Input.Action.ListUp))
            {
                _pressedUpTimer += Time.unscaledDeltaTime;
            }
            else
            {
                _nextHoldUpTime = 0f;
                _pressedUpTimer = 0f;
            }
            if (Input.IsPressed(Input.Action.ListDown))
            {
                _pressedDownTimer += Time.unscaledDeltaTime;
            }
            else
            {
                _nextHoldDownTime = 0f;
                _pressedDownTimer = 0f;
            }
            if (_pressedUpTimer > _nextHoldUpTime)
            {
                _nextHoldUpTime += _nextHoldUpTime < 0.1f ? 0.4f : 0.15f;
                return -1;
            }
            if (_pressedDownTimer > _nextHoldDownTime)
            {
                _nextHoldDownTime += _nextHoldDownTime < 0.1f ? 0.4f : 0.15f;
                return 1;
            } 
            return 0;
        }
    }
}
