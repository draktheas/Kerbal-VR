using UnityEngine;

namespace KerbalVR.Components
{
    public class KVR_ExternalCamera : MonoBehaviour
    {
        public bool IsInternalCameraEnabled {get; private set; }

        public int RenderTextureW { get; private set; }
        public int RenderTextureH { get; private set; }
        public int RenderTextureD { get; private set; }
        public RenderTextureFormat RenderTextureFormat { get; private set; }

        // this camera renders the galaxy background
        public GameObject GalaxyCameraGameObject { get; private set; }
        public Camera GalaxyCamera { get; private set; }

        // this camera renders scaled space
        public GameObject ScaledSpaceCameraGameObject { get; private set; }
        public Camera ScaledSpaceCamera { get; private set; }

        // this camera renders worlds and vessels (close)
        public GameObject Camera01GameObject { get; private set; }
        public Camera Camera01 { get; private set; }

        // this camera renders worlds and vessels (far)
        public GameObject Camera00GameObject { get; private set; }
        public Camera Camera00 { get; private set; }

        // this camera renders craft interiors
        public GameObject InternalCameraGameObject { get; private set; }
        public Camera InternalCamera { get; private set; }

        // render target
        public RenderTexture CameraRenderTexture { get; private set; }

        // camera position
        private Vector3 _position;
        public Vector3 Position {
            get { return _position; }
            set {
                _position = value;
                Camera01GameObject.transform.position = _position;
                Camera00GameObject.transform.position = _position;
                if (IsInternalCameraEnabled) {
                    InternalCameraGameObject.transform.position = 
                        InternalSpace.WorldToInternal(_position);
                }
            }
        }

        // camera rotation
        private Quaternion _rotation;
        public Quaternion Rotation {
            get { return _rotation; }
            set {
                _rotation = value;
                GalaxyCameraGameObject.transform.rotation = _rotation;
                ScaledSpaceCameraGameObject.transform.rotation = _rotation;
                Camera01GameObject.transform.rotation = _rotation;
                Camera00GameObject.transform.rotation = _rotation;
                if (IsInternalCameraEnabled) {
                    InternalCameraGameObject.transform.rotation = 
                        InternalSpace.WorldToInternal(_rotation);
                }
            }
        }

        // camera parameters
        private float _fieldOfView;
        public float FieldOfView {
            get { return _fieldOfView; }
            set {
                _fieldOfView = value;
                GalaxyCamera.fieldOfView = _fieldOfView;
                ScaledSpaceCamera.fieldOfView = _fieldOfView;
                Camera01.fieldOfView = _fieldOfView;
                Camera00.fieldOfView = _fieldOfView;
                InternalCamera.fieldOfView = _fieldOfView;
            }
        }

        private float _magnification = 1f;
        public float Magnification {
            get { return _magnification; }
            set {
                _magnification = value;
                float factor = 2f * Mathf.Tan(0.5f * 60f * Mathf.Deg2Rad);
                float zoomedFov = 2f * Mathf.Atan(factor / (2f * _magnification)) * Mathf.Rad2Deg;
                FieldOfView = zoomedFov;
            }
        }


        void Awake() {
            // initialize cameras
            GalaxyCameraGameObject = new GameObject("KVR_Camera_GalaxyCamera");
            GalaxyCameraGameObject.transform.SetParent(transform);
            GalaxyCameraGameObject.transform.position = Vector3.zero;
            GalaxyCameraGameObject.transform.rotation = Quaternion.identity;
            GalaxyCamera = GalaxyCameraGameObject.AddComponent<Camera>();
            GalaxyCamera.clearFlags = CameraClearFlags.Color;
            GalaxyCamera.backgroundColor = Color.black;
            GalaxyCamera.cullingMask = (1 << 18); // layer 18: SkySphere
            GalaxyCamera.nearClipPlane = 0.1f;
            GalaxyCamera.farClipPlane = 20f;
            GalaxyCamera.depth = -4f;
            GalaxyCamera.useOcclusionCulling = true;
            GalaxyCamera.allowHDR = false;
            GalaxyCamera.allowMSAA = true;
            GalaxyCamera.depthTextureMode = DepthTextureMode.None;
            GalaxyCamera.enabled = true;

            ScaledSpaceCameraGameObject = new GameObject("KVR_Camera_ScaledSpaceCamera");
            ScaledSpaceCameraGameObject.transform.SetParent(transform);
            ScaledSpaceCameraGameObject.transform.position = Vector3.zero;
            ScaledSpaceCameraGameObject.transform.rotation = Quaternion.identity;
            ScaledSpaceCamera = ScaledSpaceCameraGameObject.AddComponent<Camera>();
            ScaledSpaceCamera.clearFlags = CameraClearFlags.Depth;
            ScaledSpaceCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            ScaledSpaceCamera.cullingMask =
                (1 << 9) | // layer 9: Atmosphere
                (1 << 10); // layer 10: Scaled Scenery
            ScaledSpaceCamera.nearClipPlane = 1f;
            ScaledSpaceCamera.farClipPlane = 30000000f;
            ScaledSpaceCamera.depth = -3f;
            ScaledSpaceCamera.useOcclusionCulling = true;
            ScaledSpaceCamera.allowHDR = false;
            ScaledSpaceCamera.allowMSAA = true;
            ScaledSpaceCamera.depthTextureMode = DepthTextureMode.None;
            ScaledSpaceCamera.enabled = true;

            Camera01GameObject = new GameObject("KVR_Camera_Camera01");
            Camera01GameObject.transform.SetParent(transform);
            Camera01GameObject.transform.localPosition = Vector3.zero;
            Camera01GameObject.transform.localRotation = Quaternion.identity;
            Camera01 = Camera01GameObject.AddComponent<Camera>();
            Camera01.clearFlags = CameraClearFlags.Depth;
            Camera01.backgroundColor = new Color(0f, 0f, 0f, 0.02f);
            Camera01.cullingMask =
                (1 << 0) | // layer 0: Default
                (1 << 1) | // layer 1: TransparentFX
                (1 << 4) | // layer 4: Water
                (1 << 15) | // layer 15: Local Scenery
                (1 << 17) | // layer 17: EVA
                (1 << 19) | // layer 19: PhysicalObjects
                (1 << 23); // layer 23: AeroFXIgnore
            Camera01.nearClipPlane = 290f;
            Camera01.farClipPlane = 750000f;
            Camera01.depth = -1f;
            Camera01.useOcclusionCulling = true;
            Camera01.allowHDR = false;
            Camera01.allowMSAA = true;
            Camera01.depthTextureMode = DepthTextureMode.None;
            Camera01.enabled = true;

            Camera00GameObject = new GameObject("KVR_Camera_Camera00");
            Camera00GameObject.transform.SetParent(transform);
            Camera00GameObject.transform.localPosition = Vector3.zero;
            Camera00GameObject.transform.localRotation = Quaternion.identity;
            Camera00 = Camera00GameObject.AddComponent<Camera>();
            Camera00.clearFlags = CameraClearFlags.Depth;
            Camera00.backgroundColor = new Color(0f, 0f, 0f, 0.02f);
            Camera00.cullingMask =
                (1 << 0) | // layer 0: Default
                (1 << 1) | // layer 1: TransparentFX
                (1 << 4) | // layer 4: Water
                (1 << 15) | // layer 15: Local Scenery
                (1 << 17) | // layer 17: EVA
                (1 << 19) | // layer 19: PhysicalObjects
                (1 << 23); // layer 23: AeroFXIgnore
            Camera00.nearClipPlane = 0.21f;
            Camera00.farClipPlane = 300f;
            Camera00.depth = 0f;
            Camera00.useOcclusionCulling = true;
            Camera00.allowHDR = false;
            Camera00.allowMSAA = true;
            Camera00.depthTextureMode = DepthTextureMode.None;
            Camera00.enabled = true;

            InternalCameraGameObject = new GameObject("KVR_Camera_InternalCamera");
            InternalCameraGameObject.transform.SetParent(transform);
            InternalCameraGameObject.transform.localPosition = Vector3.zero;
            InternalCameraGameObject.transform.localRotation = Quaternion.identity;
            InternalCamera = InternalCameraGameObject.AddComponent<Camera>();
            InternalCamera.clearFlags = CameraClearFlags.Depth;
            InternalCamera.backgroundColor = new Color(1f, 0f, 0f, 1f);
            InternalCamera.cullingMask = (1 << 20); // layer 20: Internal
            InternalCamera.nearClipPlane = 0.05f;
            InternalCamera.farClipPlane = 300f;
            InternalCamera.depth = 0f;
            InternalCamera.useOcclusionCulling = true;
            InternalCamera.allowHDR = false;
            InternalCamera.allowMSAA = true;
            InternalCamera.depthTextureMode = DepthTextureMode.None;
            InternalCamera.enabled = true;

            // set camera parameters
            FieldOfView = 60f;
        }

        public void Init(
            bool renderInternal = true,
            int rtW = 512,
            int rtH = 512,
            int rtD = 24,
            RenderTextureFormat rtF = RenderTextureFormat.ARGB32) {
            
            RenderTextureW = rtW;
            RenderTextureH = rtH;
            RenderTextureD = rtD;
            RenderTextureFormat = rtF;

            // initialize render target
            CameraRenderTexture = new RenderTexture(
                RenderTextureW, RenderTextureH, RenderTextureD, RenderTextureFormat);
            GalaxyCamera.targetTexture = CameraRenderTexture;
            ScaledSpaceCamera.targetTexture = CameraRenderTexture;
            Camera01.targetTexture = CameraRenderTexture;
            Camera00.targetTexture = CameraRenderTexture;
            InternalCamera.targetTexture = CameraRenderTexture;
        }

        public void Render() {
            GalaxyCamera.Render();
            ScaledSpaceCamera.Render();
            Camera01.Render();
            Camera00.Render();
            if (IsInternalCameraEnabled) {
                InternalCamera.Render();
            }
        }

        public float GetFrustumHeight(float distance) {
            return 2f * distance * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
        }
    }
}
