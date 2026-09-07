using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace InputSystemActionPrompts
{


    /// <summary>
    /// Enumeration of device type
    /// TODO - Remove and use Input system types more effectively
    /// </summary>
    public enum InputDeviceType
    {
        Mouse,
        Keyboard,
        GamePad,
        Touchscreen
    }

    /// <summary>
    /// Encapsulates a binding map entry
    /// </summary>
    public class ActionBindingMapEntry
    {
        public string BindingPath;
        public bool IsComposite;
        public bool IsPartOfComposite;
        public string CompositeName;
    }

    public static class InputDevicePromptSystem
    {

        /// <summary>
        /// Map of action paths (eg "Player/Move" to binding map entries eg "Gamepad/leftStick")
        /// </summary>
        private static Dictionary<string, List<ActionBindingMapEntry>> s_ActionBindingMap = new Dictionary<string, List<ActionBindingMapEntry>>();

        /// <summary>
        /// Map of device names (eg "DualShockGamepadHID") to device prompt data (list of action bindings and sprites)
        /// </summary>
        private static Dictionary<string, InputDevicePromptData> s_DeviceDataBindingMap = new Dictionary<string, InputDevicePromptData>();

        /// <summary>
        /// Currently initialised
        /// </summary>
        private static bool s_Initialised = false;

        /// <summary>
        /// The settings file
        /// </summary>
        private static InputSystemDevicePromptSettings s_Settings;

        /// <summary>
        /// Currently active device (global, auto-detected from the last button press on ANY device).
        /// Used only by the parameterless overloads below — for split-screen / multi-context use, call the
        /// overloads that take an explicit InputDevice instead.
        /// </summary>
        private static InputDevice s_ActiveDevice;

        /// <summary>
        /// Delegate for when the global active device changes
        /// </summary>
        public static Action<InputDevice> OnActiveDeviceChanged = delegate { };

        /// <summary>
        /// Event listener for button presses on input system
        /// </summary>
        private static IDisposable s_EventListener;

        private static InputDevicePromptData s_PlatformDeviceOverride;

        public static bool GetPlatformDeviceOverride(out InputDevicePromptData inputDevice)
        {
            if (s_PlatformDeviceOverride != null)
            {
                inputDevice = s_PlatformDeviceOverride;
                return true;
            }

            // get the current platform
            var platform = Application.platform;
            // check if we have a platform override
            foreach (var platformOverride in s_Settings.RuntimePlatformsOverride)
            {
                if (platformOverride.Platform == platform)
                {
                    inputDevice = platformOverride.DevicePromptData;
                    return true;
                }
            }

            inputDevice = null;
            return false;
        }

        /// <summary>
        /// Initialises data structures and load settings, called on first use
        /// </summary>
        private static void Initialise()
        {
            Debug.Log("Initialising InputDevicePromptSystem");
            s_Settings = InputSystemDevicePromptSettings.GetSettings();

            if (s_Settings == null)
            {
                Debug.LogWarning("InputSystemDevicePromptSettings missing");
                return;
            }

            if (!s_Settings.PromptSpriteFormatter.Contains(InputSystemDevicePromptSettings.PromptSpriteFormatterSpritePlaceholder))
            {
                Debug.LogError($"{nameof(InputSystemDevicePromptSettings.PromptSpriteFormatter)} must include {InputSystemDevicePromptSettings.PromptSpriteFormatterSpritePlaceholder} or no sprites will be shown.");
            }

            // We'll want to listen to buttons being pressed on any device
            // in order to dynamically switch device prompts (From description in InputSystem.cs)
            s_EventListener = InputSystem.onAnyButtonPress.Call(OnButtonPressed);

            // Listen to device change. If the active device is disconnected, switch to default
            InputSystem.onDeviceChange += OnDeviceChange;

            BuildBindingMaps();
            FindDefaultDevice();

            GetPlatformDeviceOverride(out s_PlatformDeviceOverride);

            s_Initialised = true;
        }

        /// <summary>
        /// Called on device change
        /// </summary>
        /// <param name="device"></param>
        /// <param name="change"></param>
        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            // If the active device has been disconnected, revert to default device
            if (device != s_ActiveDevice) return;

            if ((change == InputDeviceChange.Disconnected) || (change == InputDeviceChange.Removed))
            {
                FindDefaultDevice();
                // Notify change
                OnActiveDeviceChanged.Invoke(s_ActiveDevice);
            }
        }

        /// <summary>
        /// Replace tags in a given string with TMPPro strings to insert device prompt sprites,
        /// using the global auto-detected active device.
        /// </summary>
        public static string InsertPromptSprites(string inputText) => InsertPromptSprites(inputText, s_ActiveDevice);

        /// <summary>
        /// Replace tags in a given string with TMPPro strings to insert device prompt sprites
        /// for an explicit device (use this for split-screen / multi-context UI).
        /// </summary>
        /// <param name="inputText"></param>
        /// <param name="device">The device to resolve prompts against. Pass null to fall back to "no active device" behaviour.</param>
        /// <returns></returns>
        public static string InsertPromptSprites(string inputText, InputDevice device)
        {
            if (!s_Initialised) Initialise();
            if (!s_Initialised) return "InputSystemDevicePrompt Settings missing - please create using menu item 'Window/Input System Device Prompts/Create Settings'";

            var foundTags = GetTagList(inputText);
            var replacedText = inputText;
            foreach (var tag in foundTags)
            {
                string resolvedTag;
                if (tag.StartsWith("/") && s_ActiveActionMap != null)
                    resolvedTag = $"{s_ActiveActionMap}{tag}";
                else
                    resolvedTag = tag;

                string replacementTagText = GetActionPathBindingTextSpriteTags(resolvedTag, device);

                var promptSpriteFormatter = s_Settings.PromptSpriteFormatter == "" ? InputSystemDevicePromptSettings.PromptSpriteFormatterSpritePlaceholder : s_Settings.PromptSpriteFormatter;
                promptSpriteFormatter = promptSpriteFormatter.Replace(InputSystemDevicePromptSettings.PromptSpriteFormatterSpritePlaceholder, "{0}");
                replacementTagText = string.Format(promptSpriteFormatter, replacementTagText);

                // still replace using the ORIGINAL tag, since that's what's literally in the text
                replacedText = replacedText.Replace($"{s_Settings.OpenTag}{tag}{s_Settings.CloseTag}", replacementTagText);
            }

            return replacedText;
        }

        /// <summary>
        /// Gets the first matching sprite (eg DualShock Cross Button Sprite) for the given input tag (eg "Player/Jump")
        /// using the global auto-detected active device.
        /// Currently only supports one sprite, not composite (eg WASD)
        /// </summary>
        public static Sprite GetActionPathBindingSprite(string inputTag) => GetActionPathBindingSprite(inputTag, s_ActiveDevice);

        /// <summary>
        /// Gets the first matching sprite for the given input tag against an explicit device
        /// (use this for split-screen / multi-context UI).
        /// </summary>
        /// <param name="inputTag"></param>
        /// <param name="device">The device to resolve prompts against.</param>
        /// <returns></returns>
        public static Sprite GetActionPathBindingSprite(string inputTag, InputDevice device)
        {
            if (!s_Initialised) Initialise();

            if (inputTag.StartsWith("/") && s_ActiveActionMap != null)
                inputTag = $"{s_ActiveActionMap}{inputTag}";

            var (_, matchingPrompt) = GetActionPathBindingPromptEntries(inputTag, device);
            return matchingPrompt != null && matchingPrompt.Count > 0 ? matchingPrompt[0].PromptSprite : null;
        }

        /// <summary>
        /// Gets the current (global auto-detected) active device matching sprite in DeviceSpriteEntries list for the given sprite name
        /// </summary>
        public static Sprite GetDeviceSprite(string spriteName) => GetDeviceSprite(spriteName, s_ActiveDevice);

        /// <summary>
        /// Gets the matching sprite in DeviceSpriteEntries list for the given sprite name, resolved against an
        /// explicit device (use this for split-screen / multi-context UI).
        /// </summary>
        /// <param name="spriteName"></param>
        /// <param name="device">The device to resolve the sprite against.</param>
        /// <returns></returns>
        public static Sprite GetDeviceSprite(string spriteName, InputDevice device)
        {
            if (!s_Initialised) Initialise();

            InputDevicePromptData validDevice;

            if (s_PlatformDeviceOverride != null)
            {
                validDevice = s_PlatformDeviceOverride;
            }
            else
            {
                if (device == null) return null;

                var activeDeviceName = device.name;

                if (!s_DeviceDataBindingMap.ContainsKey(activeDeviceName))
                {
                    Debug.LogError($"MISSING_DEVICE_ENTRIES '{activeDeviceName}'");
                    return null;
                }

                validDevice = s_DeviceDataBindingMap[activeDeviceName];
            }


            var matchingSprite = validDevice.DeviceSpriteEntries.FirstOrDefault((sprite) =>
                           String.Equals(sprite.SpriteName, spriteName,
                                              StringComparison.CurrentCultureIgnoreCase));

            if (matchingSprite != null)
            {
                return matchingSprite.Sprite;
            }

            return null;
        }

        /// <summary>
        /// Creates a TextMeshPro formatted string for all matching sprites for a given tag, resolved against
        /// the given device.
        /// Supports composite tags, eg WASD by returning all matches for the device (observing order)
        /// </summary>
        /// <param name="inputTag"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        private static string GetActionPathBindingTextSpriteTags(string inputTag, InputDevice device)
        {
            if (s_PlatformDeviceOverride == null) // not platform override
            {
                if (device == null) return "NO_ACTIVE_DEVICE";
                var activeDeviceName = device.name;

                if (!s_DeviceDataBindingMap.ContainsKey(activeDeviceName))
                {
                    return $"MISSING_DEVICE_ENTRIES '{activeDeviceName}'";
                }
            }

            var lowerCaseTag = inputTag.ToLower();

            if (!s_ActionBindingMap.ContainsKey(lowerCaseTag))
            {
                return $"MISSING_ACTION {lowerCaseTag}";
            }

            var (validDevice, matchingPrompt) = GetActionPathBindingPromptEntries(inputTag, device);

            if (matchingPrompt == null || matchingPrompt.Count == 0)
            {
                return $"MISSING_PROMPT '{inputTag}'";
            }
            // Return each
            var outputText = string.Empty;
            foreach (var prompt in matchingPrompt)
            {
                outputText += $"<sprite=\"{validDevice.SpriteAsset.name}\" name=\"{prompt.PromptSprite.name}\" {s_Settings.RichTextTags}>";
            }
            return outputText;
        }

        /// <summary>
        /// Gets all matching prompt entries for a given tag (eg "Player/Jump"), resolved against the given device.
        /// </summary>
        /// <param name="inputTag"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        private static (InputDevicePromptData, List<ActionBindingPromptEntry>) GetActionPathBindingPromptEntries(string inputTag, InputDevice device)
        {
            InputDevicePromptData validDevice;

            var lowerCaseTag = inputTag.ToLower();
            if (!s_ActionBindingMap.ContainsKey(lowerCaseTag)) return (null, null);

            if (s_PlatformDeviceOverride != null)
            {
                validDevice = s_PlatformDeviceOverride;
            }
            else
            {
                if (device == null) return (null, null);
                if (!s_DeviceDataBindingMap.ContainsKey(device.name)) return (null, null);

                validDevice = s_DeviceDataBindingMap[device.name];
            }

            var validEntries = new List<ActionBindingPromptEntry>();
            var actionBindings = s_ActionBindingMap[lowerCaseTag];

            foreach (var actionBinding in actionBindings)
            {
                // Composite bindings (eg WASD, D-Pad) resolve as a single collapsed icon, matched by the
                // composite's name rather than by any individual part's physical control path. Author a
                // prompt entry with ActionBindingPath "Composite/<Name>" (eg "Composite/WASD") in the
                // relevant device's InputDevicePromptData asset to supply that icon.
                if (actionBinding.IsComposite)
                {
                    if (string.IsNullOrEmpty(actionBinding.CompositeName))
                        continue;

                    var compositeKey = $"Composite/{actionBinding.CompositeName}";
                    var matchingCompositePrompt = validDevice.ActionBindingPromptEntries.FirstOrDefault((prompt) =>
                        String.Equals(prompt.ActionBindingPath, compositeKey, StringComparison.CurrentCultureIgnoreCase));
                    if (matchingCompositePrompt != null)
                    {
                        validEntries.Add(matchingCompositePrompt);
                    }
                    continue;
                }

                //Debug.Log($"Checking binding '{actionBinding}' on device {validDevice.name}");
                var usage = GetUsageFromBindingPath(actionBinding.BindingPath);
                if (string.IsNullOrEmpty(usage))
                {
                    var matchingPrompt = validDevice.ActionBindingPromptEntries.FirstOrDefault((prompt) =>
                        String.Equals(prompt.ActionBindingPath, actionBinding.BindingPath,
                            StringComparison.CurrentCultureIgnoreCase));
                    if (matchingPrompt != null)
                    {
                        //Debug.Log($"Found matching prompt {matchingPrompt.ActionBindingPath} for {inputTag}");
                        validEntries.Add(matchingPrompt);
                    }
                }
                else
                {
                    // This is a usage, eg "Submit" or "Cancel", in the format "*/{Submit}"

                    // Its possible in some control schemes (eg mouse keyboard) that the given device
                    // Doesnt have a given usage (eg submit), so will want to find an alternative

                    var matchingUsageFound = false;
                    var deviceList = new List<InputDevice>(InputSystem.devices);
                    // Move the given device to front of queue
                    deviceList.Remove(device);
                    deviceList.Insert(0, device);

                    for (var i = 0; i < deviceList.Count && !matchingUsageFound; i++)
                    {
                        var testDevice = deviceList[i];
                        foreach (var control in testDevice.allControls)
                        {
                            foreach (var controlUsage in control.usages)
                            {
                                if (controlUsage.ToLower() == usage.ToLower())
                                {
                                    // Match! Search for prompt entry with same extension (ignore first part eg "gamepad")
                                    var matchingPrompt = validDevice.ActionBindingPromptEntries.FirstOrDefault((prompt) =>
                                        String.Equals(prompt.ActionBindingPath.Split('/').Last(), control.name,
                                            StringComparison.CurrentCultureIgnoreCase));
                                    if (matchingPrompt != null)
                                    {
                                        validEntries.Add(matchingPrompt);
                                        matchingUsageFound = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return (validDevice, validEntries);
        }

        /// <summary>
        /// Extract the usage from a binding path, eg "*/{Submit}" returns "Submit"
        /// </summary>
        /// <param name="actionBinding"></param>
        /// <returns></returns>
        private static string GetUsageFromBindingPath(string actionBinding)
        {
            return actionBinding.Contains("*/{") ? actionBinding.Substring(3, actionBinding.Length - 4) : String.Empty;
        }

        /// <summary>
        /// Extracts all tags from a given string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static List<string> GetTagList(string input)
        {
            var outputTags = new List<string>();
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == s_Settings.OpenTag)
                {
                    var start = i + 1;
                    var end = input.IndexOf(s_Settings.CloseTag, i + 1);
                    var foundTag = input.Substring(start, end - start);
                    outputTags.Add(foundTag);
                }
            }

            return outputTags;
        }


        /// <summary>
        /// Finds default device based on current settings priorities
        /// </summary>
        private static void FindDefaultDevice()
        {
            // When we start up there have been no button presses, so we want to pick the first device
            // that matches the priorities in the settings file

            foreach (var deviceType in s_Settings.DefaultDevicePriority)
            {
                foreach (var device in InputSystem.devices.Where(device => DeviceMatchesType(device, deviceType)))
                {
                    s_ActiveDevice = device;
                    return;
                }
            }
        }

        private static bool DeviceMatchesType(InputDevice device, InputDeviceType type)
        {
            return type switch
            {
                InputDeviceType.Mouse => device is Mouse,
                InputDeviceType.Keyboard => device is Keyboard,
                InputDeviceType.GamePad => device is Gamepad,
                InputDeviceType.Touchscreen => device is Touchscreen,
                _ => false
            };
        }


        /// <summary>
        /// Builds internal map of all actions (eg "Player/Jump" to available binding paths (eg "Gamepad/ButtonSouth")
        /// </summary>
        private static void BuildBindingMaps()
        {
            s_ActionBindingMap = new Dictionary<string, List<ActionBindingMapEntry>>();

            // Build a map of all controls and associated bindings
            foreach (var inputActionAsset in s_Settings.InputActionAssets)
            {
                var allActionMaps = inputActionAsset.actionMaps;
                foreach (var actionMap in allActionMaps)
                {
                    // Tracks the name of the composite (eg "WASD") whose part rows we're currently
                    // walking through, since part rows immediately follow their composite header row.
                    string currentCompositeName = null;

                    foreach (var binding in actionMap.bindings)
                    {
                        var bindingPath = $"{actionMap.name}/{binding.action}";
                        var bindingPathLower = bindingPath.ToLower();

                        if (binding.isComposite)
                            currentCompositeName = binding.name;

                        // Skip individual composite part rows (eg the separate W/A/S/D or D-Pad Up/Down/Left/Right
                        // bindings) entirely - we only want ONE entry representing the whole composite, so it
                        // resolves to a single icon rather than one icon per physical key/button.
                        if (binding.isPartOfComposite)
                            continue;

                        //Debug.Log($"Binding {bindingPathLower} to path {binding.path}");
                        var entry = new ActionBindingMapEntry
                        {
                            BindingPath = binding.effectivePath,
                            IsComposite = binding.isComposite,
                            IsPartOfComposite = binding.isPartOfComposite,
                            CompositeName = binding.isComposite ? currentCompositeName : null
                        };
                        if (s_ActionBindingMap.TryGetValue(bindingPathLower, out var value))
                        {
                            value.Add(entry);
                        }
                        else
                        {
                            s_ActionBindingMap.Add(bindingPathLower, new List<ActionBindingMapEntry> { entry });
                        }
                    }
                }
            }


            // Build a map of device name to device data
            foreach (var devicePromptData in s_Settings.DevicePromptAssets)
            {
                foreach (var deviceName in devicePromptData.DeviceNames)
                {
                    if (s_DeviceDataBindingMap.ContainsKey(deviceName))
                    {
                        Debug.LogWarning(
                            $"Duplicate device name found in InputSystemDevicePromptSettings: {deviceName}. Check your entries");
                    }
                    else
                    {
                        s_DeviceDataBindingMap.Add(deviceName, devicePromptData);
                    }
                }
            }
        }

        /// <summary>
        /// Called when a button is pressed on any device. Drives the global auto-detected active device
        /// used by the parameterless overloads.
        /// </summary>
        /// <param name="button"></param>
        private static void OnButtonPressed(InputControl button)
        {
            if (s_ActiveDevice == button.device) return;
            s_ActiveDevice = button.device;
            OnActiveDeviceChanged.Invoke(s_ActiveDevice);
        }


        /// <summary>
        /// The action map currently considered "active" for tags written without
        /// an explicit map prefix (for example [/BabyJump] instead of [SameDevice/BabyJump]).
        /// </summary>
        private static string s_ActiveActionMap;

        /// <summary>
        /// Fired when the active action map alias changes (independent of device changes)
        /// </summary>
        public static Action OnActionMapChanged = delegate { };

        /// <summary>
        /// To call whenever your game logic switches control scheme / action map
        /// (SameDevice -> DifferentDevice and so on)
        /// By the way I'm at max productivity when I listen to this https://youtu.be/GX5gLAin-x4
        /// If anyone says it's bad I'm quitting the team
        /// </summary>
        public static void SetActiveActionMap(string actionMapName)
        {
            if (!s_Initialised) Initialise();
            if (s_ActiveActionMap == actionMapName) return;
            s_ActiveActionMap = actionMapName;
            OnActionMapChanged.Invoke();
        }
    }
}
