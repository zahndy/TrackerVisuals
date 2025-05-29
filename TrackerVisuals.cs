using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FrooxEngine;
using ResoniteModLoader;
using HarmonyLib;
using Elements.Core;
using FrooxEngine.CommonAvatar;
using static FrooxEngine.TrackerSettings;
using SkyFrost.Base;

namespace TrackerVisuals
{
    public class TrackerVisuals : ResoniteMod
    {
        public override String Name => "TrackerVisuals";
        public override String Author => "zahndy";
        public override String Link => "https://github.com/zahndy/TrackerVisuals";
        public override String Version => "1.0.0";

        public static ModConfiguration config;

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> ENABLED = new ModConfigurationKey<bool>("enabled", "Enabled", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<dummy> DUMMY_ = new ModConfigurationKey<dummy>("dummy_", $"<size=300>Enter Tracker ID's you wish to hide the visual of. " +
            $"\nTo grab the ID you need to have the tracker Turned On and then you can find them in your User Root. " +
            $"\n models can be replaced with custom ones by putting the resrec in the other field, they will be matched on index</size> ");

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<string> TrackersCsv = new ModConfigurationKey<string>("TrackersCsv", "List of Tracker id's(csv of id's: \"ID1,ID2,ID3\")", () => "");

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<string> CustomModels = new ModConfigurationKey<string>("CustomModels", "CustomModels list (csv of resrecs)", () => ""); 

        private static Dictionary<string, TrackedDevicePositioner> trackedDevices = new Dictionary<string, TrackedDevicePositioner>();

        private static readonly Uri TrackerModel = new Uri("resdb:///2a0a95770bdd7e6da335de03d3483a630f9dc5a80c12b673804017c8ffe7eedc.brson"); // Default model for trackers
        public override void OnEngineInit()

        {
            config = GetConfiguration();
            config.Save(true);
            Harmony harmony = new Harmony("com.zahndy.TrackerVisuals");
            harmony.PatchAll();
        }

        private void OnThisConfigurationChanged(ConfigurationChangedEvent configurationChangedEvent)
        {
            if (configurationChangedEvent.Key == ENABLED)
            {


                if (configurationChangedEvent.Key == TrackersCsv)
                {
                    string trackercsvStr = config.GetValue(TrackersCsv).Trim(',', ' ');
                    List<String> trackercsv = trackercsvStr.Split(',').ToList();
                    foreach (KeyValuePair<string, TrackedDevicePositioner> trackedDevice in trackedDevices)
                    {
                        if (trackercsv.Contains(trackedDevice.Key))
                        {
                            TrackedDevicePositioner trackedDevicePositioner = trackedDevice.Value;
                            if (trackedDevicePositioner.ReferenceModel.Target != null)
                            {
                                trackedDevicePositioner.ReferenceModel.Target.Destroy();
                                trackedDevice.Value.ReferenceModel.Target = trackedDevice.Value.Slot.AddSlot("Model");
                                if (config.GetValue(CustomModels).Length > 0)
                                {
                                    List<string> customModels = config.GetValue(CustomModels).Split(',').ToList();
                                    int trackerIndex = trackercsv.IndexOf(trackedDevice.Key);
                                    if (trackerIndex >= 0 && trackerIndex < customModels.Count)
                                    {
                                        Uri TrModel = new Uri(customModels[trackerIndex]);
                                        trackedDevicePositioner.ReferenceModel.Target.RunSynchronously(() =>
                                        {
                                            trackedDevicePositioner.ReferenceModel.Target.LoadObjectAsync(TrModel);
                                        });
                                    }
                                    else
                                    {
                                        trackedDevicePositioner.ReferenceModel.Target.LoadObjectAsync(TrackerModel);
                                    }
                                }
                                else
                                {
                                    trackedDevicePositioner.ReferenceModel.Target.LoadObjectAsync(TrackerModel);
                                    //trackedDevice.Value.ReferenceModel.Target.LoadObjectAsync(TrackerModel);
                                }
                                //trackedDevicePositioner.ReferenceModel.Target = null;
                                // trackedDevicePositioner.ReferenceModel.Target.ActiveSelf = false;
                            }
                        }
                        else
                        {
                            if (trackedDevice.Value.ReferenceModel.Target == null)
                            {
                                trackedDevice.Value.ReferenceModel.Target = trackedDevice.Value.Slot.AddSlot("Model");
                                trackedDevice.Value.ReferenceModel.Target.LoadObjectAsync(TrackerModel);
                            }
                            else
                            {
                                trackedDevice.Value.ReferenceModel.Target.ActiveSelf = true;
                            }
                        }
                    }

                }
            }
        }

        [HarmonyPatch(typeof(TrackerController))]
        class TrackerController_Patch
        {
            [HarmonyPrefix]
            [HarmonyPatch("OnInputDeviceAdded")]
            static bool Prefix(TrackerController __instance, IInputDevice obj)
            {
                ITracker tracker = obj as ITracker;
                if (tracker == null)
                {
                    return false;
                }
                __instance.RunSynchronously(delegate
                {
                    FrooxEngine.User localUser = __instance.LocalUser;
                    Slot slot = localUser.Root.Slot.AddSlot(tracker.PublicID);
                    TrackedDevicePositioner trackedDevicePositioner = slot.AttachComponent<TrackedDevicePositioner>();

                    if (!trackedDevices.ContainsKey(tracker.PublicID))
                    {
                        trackedDevices.Add(tracker.PublicID, trackedDevicePositioner);
                    }

                    TransformStreamDriver transformStreamDriver = slot.AttachComponent<TransformStreamDriver>();
                    BodyNode correspondingBodyNode = tracker.CorrespondingBodyNode;
                    if (correspondingBodyNode != 0 && !FullBodyCalibrator.IsMappableBodyNode(correspondingBodyNode))
                    {
                        trackedDevicePositioner.CreateAvatarObjectSlot.Value = false;
                    }

                    bool created;
                    ValueStream<float3> streamOrAdd = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<float3>>(localUser, tracker.PublicID + ".Pos", out created);
                    bool created2;
                    ValueStream<floatQ> streamOrAdd2 = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<floatQ>>(localUser, tracker.PublicID + ".Rot", out created2);
                    if (created)
                    {
                        __instance.PositionStreamConfigurator.Target?.Invoke(streamOrAdd, 2);

                    }

                    if (created2)
                    {
                        __instance.RotationStreamConfigurator.Target?.Invoke(streamOrAdd2, 2);
                    }

                    trackedDevicePositioner.DeviceIndex.Value = tracker.DeviceIndex;
                    transformStreamDriver.PositionStream.Target = streamOrAdd;
                    transformStreamDriver.RotationStream.Target = streamOrAdd2;
                    transformStreamDriver.Position.Target = slot.Position_Field;
                    transformStreamDriver.Rotation.Target = slot.Rotation_Field;
                    List<string> TrackerIDs = config.GetValue(TrackersCsv).Split(',').ToList();
                    if (tracker.DisplayModel != null)
                    {
                        Slot slot2 = slot.AddSlot("Model");
                        if (TrackerIDs.Contains(tracker.PublicID))
                        {
                            if (config.GetValue(CustomModels).Length > 0)
                            {
                                List<string> customModels = config.GetValue(CustomModels).Split(',').ToList();
                                string trackercsvStr = config.GetValue(TrackersCsv).Trim(',', ' ');
                                List<String> trackercsv = trackercsvStr.Split(',').ToList();

                                int trackerIndex = trackercsv.IndexOf(tracker.PublicID);
                                if (trackerIndex >= 0 && trackerIndex < customModels.Count)
                                {
                                    Uri TrModel = new Uri(customModels[trackerIndex]);
                                    trackedDevicePositioner.ReferenceModel.Target = slot2;
                                    slot2.LoadObjectAsync(TrModel);
                                }
                            }
                        }
                        else
                        {
                            trackedDevicePositioner.ReferenceModel.Target = slot2;
                            slot2.LoadObjectAsync(tracker.DisplayModel);
                        }
                    }
                });
                return false;
            }
        }
    }
}
