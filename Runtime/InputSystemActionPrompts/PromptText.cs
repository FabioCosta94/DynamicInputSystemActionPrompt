using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InputSystemActionPrompts
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class PromptText : MonoBehaviour
    {
        [SerializeField] private InputActionAsset m_InputActionAsset;

        /// <summary>
        /// Which side of the screen / character this text belongs to.
        /// Leave as Auto for single-player UI (uses the package's global auto-detected device).
        /// Set to Baby or Mother for split-screen prompts pinned to that character's assigned device.
        /// </summary>
        [SerializeField] private PromptContextId m_Context = PromptContextId.Auto;

        /// <summary>
        /// Cached TextMeshProUGUI component that we'll apply the prompt sprites to
        /// </summary>
        private TextMeshProUGUI m_TextField;

        /// <summary>
        /// Cached original text, so we can reapply it if the input device changes
        /// </summary>
        private string m_OriginalText;


        void Start()
        {
            m_TextField = GetComponent<TextMeshProUGUI>();
            if (m_TextField == null) return;
            m_OriginalText = m_TextField.text;
            RefreshText();

            if (m_Context == PromptContextId.Auto)
                InputDevicePromptSystem.OnActiveDeviceChanged += DeviceChanged;
            else
                InputDevicePromptContextRegistry.DeviceChanged += ContextDeviceChanged;

            InputDevicePromptSystem.OnActionMapChanged += RefreshText;
        }

        private void OnDestroy()
        {
            if (m_Context == PromptContextId.Auto)
                InputDevicePromptSystem.OnActiveDeviceChanged -= DeviceChanged;
            else
                InputDevicePromptContextRegistry.DeviceChanged -= ContextDeviceChanged;

            InputDevicePromptSystem.OnActionMapChanged -= RefreshText;
        }

        /// <summary>
        /// Called when the global auto-detected active device changes (Auto context only)
        /// </summary>
        private void DeviceChanged(InputDevice device)
        {
            RefreshText();
        }

        /// <summary>
        /// Called when a context-registry device changes; only react if it's our context
        /// </summary>
        private void ContextDeviceChanged(PromptContextId context)
        {
            if (context == m_Context)
                RefreshText();
        }

        /// <summary>
        /// Applies text with prompt sprites to the TextMeshProUGUI component
        /// </summary>
        private void RefreshText()
        {
            if (m_TextField == null) return;

            m_TextField.text = m_Context == PromptContextId.Auto
                ? InputDevicePromptSystem.InsertPromptSprites(m_OriginalText)
                : InputDevicePromptSystem.InsertPromptSprites(m_OriginalText, InputDevicePromptContextRegistry.GetDevice(m_Context));
        }
    }
}
