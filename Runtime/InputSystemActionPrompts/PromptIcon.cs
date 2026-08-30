using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InputSystemActionPrompts
{
    [RequireComponent(typeof(Image))]
    public class PromptIcon : MonoBehaviour
    {
        /// <summary>
        /// This should be the full path, including binding map and action, eg "Player/Move"
        /// </summary>
        [SerializeField] private string m_Action = "Player/Move";

        /// <summary>
        /// Which side of the screen / character this icon belongs to.
        /// Leave as Auto for single-player UI (uses the package's global auto-detected device).
        /// Set to Baby or Mother for split-screen prompts pinned to that character's assigned device.
        /// </summary>
        [SerializeField] private PromptContextId m_Context = PromptContextId.Auto;

        /// <summary>
        /// The image to apply the prompt sprite to
        /// </summary>
        private Image m_Image;

        [SerializeField] private bool _setNativeSize = true;

        void Start()
        {
            m_Image = GetComponent<Image>();
            if (m_Image == null) return;
            RefreshIcon();

            if (m_Context == PromptContextId.Auto)
                InputDevicePromptSystem.OnActiveDeviceChanged += DeviceChanged;
            else
                InputDevicePromptContextRegistry.DeviceChanged += ContextDeviceChanged;

            InputDevicePromptSystem.OnActionMapChanged += RefreshIcon;
        }

        private void OnDestroy()
        {
            if (m_Context == PromptContextId.Auto)
                InputDevicePromptSystem.OnActiveDeviceChanged -= DeviceChanged;
            else
                InputDevicePromptContextRegistry.DeviceChanged -= ContextDeviceChanged;

            InputDevicePromptSystem.OnActionMapChanged -= RefreshIcon;
        }

        /// <summary>
        /// Called when the global auto-detected active device changes (Auto context only)
        /// </summary>
        private void DeviceChanged(InputDevice device)
        {
            RefreshIcon();
        }

        /// <summary>
        /// Called when a context-registry device changes; only react if it's our context
        /// </summary>
        private void ContextDeviceChanged(PromptContextId context)
        {
            if (context == m_Context)
                RefreshIcon();
        }

        /// <summary>
        /// Sets the icon for the current action
        /// </summary>
        private void RefreshIcon()
        {
            var sourceSprite = m_Context == PromptContextId.Auto
                ? InputDevicePromptSystem.GetActionPathBindingSprite(m_Action)
                : InputDevicePromptSystem.GetActionPathBindingSprite(m_Action, InputDevicePromptContextRegistry.GetDevice(m_Context));

            if (sourceSprite == null) return;
            m_Image.sprite = sourceSprite;

            if (_setNativeSize)
                m_Image.SetNativeSize();
        }
    }
}
