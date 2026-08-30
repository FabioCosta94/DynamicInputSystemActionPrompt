using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InputSystemActionPrompts
{
    [RequireComponent(typeof(Image))]
    public class DeviceSpriteSwap : MonoBehaviour
    {

        /// <summary>
        /// The image to apply the prompt sprite to
        /// </summary>
        private Image m_Image;

        /// <summary>
        /// The name of the custom sprite to use
        /// </summary>
        [SerializeField] private string customSpriteName = "";

        /// <summary>
        /// Which side of the screen / character this sprite belongs to.
        /// Leave as Auto for single-player UI (uses the package's global auto-detected device).
        /// Set to Baby or Mother for split-screen prompts pinned to that character's assigned device.
        /// </summary>
        [SerializeField] private PromptContextId m_Context = PromptContextId.Auto;

        [SerializeField] private bool _setNativeSize = true;

        void Start()
        {
            m_Image = GetComponent<Image>();
            if (m_Image == null) return;
            RefreshSprite();

            if (m_Context == PromptContextId.Auto)
                InputDevicePromptSystem.OnActiveDeviceChanged += DeviceChanged;
            else
                InputDevicePromptContextRegistry.DeviceChanged += ContextDeviceChanged;
        }

        private void OnDestroy()
        {
            if (m_Context == PromptContextId.Auto)
                InputDevicePromptSystem.OnActiveDeviceChanged -= DeviceChanged;
            else
                InputDevicePromptContextRegistry.DeviceChanged -= ContextDeviceChanged;
        }

        /// <summary>
        /// Called when the global auto-detected active device changes (Auto context only)
        /// </summary>
        private void DeviceChanged(InputDevice device)
        {
            RefreshSprite();
        }

        /// <summary>
        /// Called when a context-registry device changes; only react if it's our context
        /// </summary>
        private void ContextDeviceChanged(PromptContextId context)
        {
            if (context == m_Context)
                RefreshSprite();
        }

        /// <summary>
        /// Sets the icon for the current action
        /// </summary>
        private void RefreshSprite()
        {
            var sourceSprite = m_Context == PromptContextId.Auto
                ? InputDevicePromptSystem.GetDeviceSprite(customSpriteName)
                : InputDevicePromptSystem.GetDeviceSprite(customSpriteName, InputDevicePromptContextRegistry.GetDevice(m_Context));

            if (sourceSprite == null) return;

            m_Image.sprite = sourceSprite;
            if (_setNativeSize)
                m_Image.SetNativeSize();
        }

    }
}
