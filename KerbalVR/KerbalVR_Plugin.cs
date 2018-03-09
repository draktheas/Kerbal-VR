using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using Valve.VR;

namespace KerbalVR
{
    // Start plugin on starting the game
    //
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KerbalVR_Plugin : MonoBehaviour
    {
        // this function allows importing DLLs from a given path
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        // define location of OpenVR library
        public static string OpenVRDllPath {
            get {
                string currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string openVrPath = Path.Combine(currentPath, "openvr");
                return Path.Combine(openVrPath, Utils.Is64BitProcess ? "win64" : "win32");
            }
        }

        private bool hmdIsInitialized = false;
        private bool hmdIsEnabled = false;

        private CVRSystem vrSystem;
        private CVRCompositor vrCompositor;
        private TrackedDevicePose_t[] vrDevicePoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        private TrackedDevicePose_t[] vrRenderPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        private TrackedDevicePose_t[] vrGamePoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        private VRControllerState_t ctrlStateLeft = new VRControllerState_t();
        private VRControllerState_t ctrlStateRight = new VRControllerState_t();
        private uint ctrlStateLeft_lastPacketNum, ctrlStateRight_lastPacketNum;
        private uint ctrlIndexLeft = 0;
        private uint ctrlIndexRight = 0;

        private Texture_t hmdLeftEyeTexture, hmdRightEyeTexture;
        private VRTextureBounds_t hmdTextureBounds;
        private RenderTexture hmdLeftEyeRenderTexture, hmdRightEyeRenderTexture;

        // list of all cameras in the game
        //--------------------------------------------------------------
        private string[] cameraNames = {
            "GalaxyCamera",
            "Camera ScaledSpace",
            "Camera 01",
            "Camera 00",
            "InternalCamera",
            // "UIMainCamera",
            // "UIVectorCamera",
        };

        // struct to keep track of Camera properties
        private struct CameraProperties
        {
            public Camera camera;
            public Matrix4x4 originalProjMatrix;
            public Matrix4x4 hmdLeftProjMatrix;
            public Matrix4x4 hmdRightProjMatrix;

            public CameraProperties(Camera camera, Matrix4x4 originalProjMatrix, Matrix4x4 hmdLeftProjMatrix, Matrix4x4 hmdRightProjMatrix)
            {
                this.camera = camera;
                this.originalProjMatrix = originalProjMatrix;
                this.hmdLeftProjMatrix = hmdLeftProjMatrix;
                this.hmdRightProjMatrix = hmdRightProjMatrix;
            }
        }

        // list of cameras to render (Camera objects)
        private List<CameraProperties> camerasToRender;

        private GameObject vrCameraLeftGameObject, vrCameraRightGameObject;
        private Camera vrCameraLeft, vrCameraRight;
        private KerbalVR_Renderer vrRendererLeft, vrRendererRight;


        /// <summary>
        /// Overrides the Start method for a MonoBehaviour plugin.
        /// </summary>
        void Start()
        {
            Utils.LogInfo("KerbalVrPlugin started.");

            InitHMD();
            InitVRScene();

            DontDestroyOnLoad(this);
        }

        /// <summary>
        /// Overrides the Update method, called every frame.
        /// </summary>
        void LateUpdate()
        {
            // start HMD using the Y key
            if (Input.GetKeyDown(KeyCode.Y)) {
                hmdIsEnabled = !hmdIsEnabled;
                vrRendererLeft.hmdIsEnabled = hmdIsEnabled;
                vrRendererRight.hmdIsEnabled = hmdIsEnabled;
                Utils.LogInfo("HMD enabled: " + hmdIsEnabled);
            }

            // track VR device poses
            if (hmdIsInitialized && hmdIsEnabled) {
                EVRCompositorError vrCompositorError = EVRCompositorError.None;

                vrSystem.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseSeated, 0.0f, vrDevicePoses);
                HmdMatrix34_t vrLeftEyeTransform = vrSystem.GetEyeToHeadTransform(EVREye.Eye_Left);
                HmdMatrix34_t vrRightEyeTransform = vrSystem.GetEyeToHeadTransform(EVREye.Eye_Right);
                vrCompositorError = vrCompositor.WaitGetPoses(vrRenderPoses, vrGamePoses);

                if (vrCompositorError != EVRCompositorError.None) {
                    Utils.LogWarning("WaitGetPoses failed: " + (int)vrCompositorError);
                    hmdIsEnabled = false;
                    return;
                }

                // convert SteamVR poses to Unity coordinates
                var hmdTransform = new SteamVR_Utils.RigidTransform(vrDevicePoses[OpenVR.k_unTrackedDeviceIndex_Hmd].mDeviceToAbsoluteTracking);
                var hmdLeftEyeTransform = new SteamVR_Utils.RigidTransform(vrLeftEyeTransform);
                var hmdRightEyeTransform = new SteamVR_Utils.RigidTransform(vrRightEyeTransform);
                var ctrlPoseLeft = new SteamVR_Utils.RigidTransform(vrDevicePoses[ctrlIndexLeft].mDeviceToAbsoluteTracking);
                var ctrlPoseRight = new SteamVR_Utils.RigidTransform(vrDevicePoses[ctrlIndexRight].mDeviceToAbsoluteTracking);
                
            }
        }
        

        /// <summary>
        /// Overrides the OnDestroy method, called when plugin is destroyed (leaving Flight scene).
        /// </summary>
        void OnDestroy()
        {
            Utils.LogInfo("KerbalVrPlugin OnDestroy");
            OpenVR.Shutdown();
            hmdIsInitialized = false;
        }


        /// <summary>
        /// Initialize HMD using OpenVR API calls.
        /// </summary>
        /// <returns>True on success, false otherwise. Errors logged.</returns>
        bool InitHMD()
        {
            bool retVal = false;

            // return if HMD has already been initialized
            if (hmdIsInitialized)
            {
                return true;
            }

            // set the location of the OpenVR DLL
            SetDllDirectory(OpenVRDllPath);

            // check if HMD is connected on the system
            retVal = OpenVR.IsHmdPresent();
            if (!retVal)
            {
                Utils.LogError("HMD not found on this system.");
                return retVal;
            }

            // check if SteamVR runtime is installed.
            // For this plugin, MAKE SURE IT IS ALREADY RUNNING.
            retVal = OpenVR.IsRuntimeInstalled();
            if (!retVal)
            {
                Utils.LogError("SteamVR runtime not found on this system.");
                return retVal;
            }

            // initialize HMD
            EVRInitError hmdInitErrorCode = EVRInitError.None;
            vrSystem = OpenVR.Init(ref hmdInitErrorCode, EVRApplicationType.VRApplication_Scene);

            // return if failure
            retVal = (hmdInitErrorCode == EVRInitError.None);
            if (!retVal)
            {
                Utils.LogError("Failed to initialize HMD. Init returned: " + OpenVR.GetStringForHmdError(hmdInitErrorCode));
                return retVal;
            }
            else
            {
                Utils.LogInfo("OpenVR.Init passed.");
            }
            
            // reset "seated position" and capture initial position. this means you should hold the HMD in
            // the position you would like to consider "seated", before running this code.

            ResetInitialHmdPosition();

            // initialize Compositor
            vrCompositor = OpenVR.Compositor;

            // initialize render textures (for displaying on HMD)
            uint renderTextureWidth = 0;
            uint renderTextureHeight = 0;
            vrSystem.GetRecommendedRenderTargetSize(ref renderTextureWidth, ref renderTextureHeight);

            Utils.LogInfo("Render Texture size: " + renderTextureWidth + " x " + renderTextureHeight);

            hmdLeftEyeRenderTexture = new RenderTexture((int)renderTextureWidth, (int)renderTextureHeight, 24, RenderTextureFormat.ARGB32);
            hmdLeftEyeRenderTexture.Create();

            hmdRightEyeRenderTexture = new RenderTexture((int)renderTextureWidth, (int)renderTextureHeight, 24, RenderTextureFormat.ARGB32);
            hmdRightEyeRenderTexture.Create();

            hmdLeftEyeTexture.handle = hmdLeftEyeRenderTexture.GetNativeTexturePtr();
            hmdLeftEyeTexture.eColorSpace = EColorSpace.Auto;

            hmdRightEyeTexture.handle = hmdRightEyeRenderTexture.GetNativeTexturePtr();
            hmdRightEyeTexture.eColorSpace = EColorSpace.Auto;
            
            var gfxAPI = ETextureType.OpenGL;

            switch (SystemInfo.graphicsDeviceType)
            {
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore:
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2:
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3:
                    gfxAPI = ETextureType.OpenGL;
                    break; //doesnt work in unity 5.4 with current SteamVR (02/2018)
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D9:
                    throw (new Exception("DirectX9 not supported"));
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D11:
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D12:
                    gfxAPI = ETextureType.DirectX;
                    break;
                default:
                    throw (new Exception(SystemInfo.graphicsDeviceType.ToString() + " not supported"));
            }

            hmdLeftEyeTexture.eType = gfxAPI;
            hmdRightEyeTexture.eType = gfxAPI;

            // Set rendering bounds on texture to render?
            // I assume min=0.0 and max=1.0 renders to the full extent of the texture
            hmdTextureBounds.uMin = 0.0f;
            hmdTextureBounds.uMax = 1.0f;
            hmdTextureBounds.vMin = 0.0f;
            hmdTextureBounds.vMax = 1.0f;
            // TODO: Need to understand better how to create render targets and incorporate hidden area mask mesh
            
            Utils.LogInfo("HMD Init finished");

            hmdIsInitialized = true;

            return retVal;
        }

        void InitVRScene() {
            vrCameraLeftGameObject = new GameObject("VRCamera L");
            vrCameraLeft = Utils.GetOrAddComponent<Camera>(vrCameraLeftGameObject) as Camera;
            vrRendererLeft = Utils.GetOrAddComponent<KerbalVR_Renderer>(vrCameraLeftGameObject) as KerbalVR_Renderer;

            vrRendererLeft.hmdIsInitialized = hmdIsInitialized;
            vrRendererLeft.vrCompositor = vrCompositor;
            vrRendererLeft.hmdEye = EVREye.Eye_Left;
            vrCameraLeft.enabled = true;


            vrCameraRightGameObject = new GameObject("VRCamera R");
            vrCameraRight = Utils.GetOrAddComponent<Camera>(vrCameraRightGameObject) as Camera;
            vrRendererRight = Utils.GetOrAddComponent<KerbalVR_Renderer>(vrCameraRightGameObject) as KerbalVR_Renderer;

            vrRendererRight.hmdIsInitialized = hmdIsInitialized;
            vrRendererRight.vrCompositor = vrCompositor;
            vrRendererRight.hmdEye = EVREye.Eye_Right;
            vrCameraRight.enabled = true;
        }
        
        /// <summary>
        /// Sets the current real-world position of the HMD as the seated origin in IVA.
        /// </summary>
        void ResetInitialHmdPosition()
        {
            if (hmdIsInitialized)
            {
                vrSystem.ResetSeatedZeroPose();
                Utils.LogInfo("Seated pose reset!");
            }
        }

    } // class KerbalVR_Plugin
} // namespace KerbalVR
