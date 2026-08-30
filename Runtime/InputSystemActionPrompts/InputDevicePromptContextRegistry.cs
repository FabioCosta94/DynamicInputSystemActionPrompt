using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace InputSystemActionPrompts
{
    /// <summary>
    /// Which "side of the screen" a prompt belongs to.
    /// Auto means "use the package's global auto-detected active device" (unchanged legacy behaviour,
    /// good for single-player UI, main menu, settings screen, etc).
    /// Baby / Mother are pinned explicitly by your game code (eg DeviceManager) rather than
    /// auto-detected from button presses, since in split-screen you don't want one character's
    /// icons flipping because the other character pressed a button on their device.
    /// </summary>
    public enum PromptContextId
    {
        Auto,
        Baby,
        Mother
    }

    /// <summary>
    /// Tracks one InputDevice per PromptContextId, and notifies listeners when a given context's
    /// device changes. Your game logic (eg DeviceManager) is responsible for calling SetDevice
    /// whenever it assigns/reassigns a device to a character. PromptText/PromptIcon/DeviceSpriteSwap
    /// components tagged with a non-Auto context read from here instead of the package's global
    /// active device.
    /// </summary>
    public static class InputDevicePromptContextRegistry
    {
        public static event Action<PromptContextId> DeviceChanged = delegate { };

        private static readonly Dictionary<PromptContextId, InputDevice> s_Devices = new();

        public static void SetDevice(PromptContextId context, InputDevice device)
        {
            if (context == PromptContextId.Auto)
                return; // Auto is driven by InputDevicePromptSystem itself, not this registry

            if (s_Devices.TryGetValue(context, out var current) && current == device)
                return;

            s_Devices[context] = device;
            DeviceChanged.Invoke(context);
        }

        public static InputDevice GetDevice(PromptContextId context)
            => s_Devices.TryGetValue(context, out var device) ? device : null;
    }
}
