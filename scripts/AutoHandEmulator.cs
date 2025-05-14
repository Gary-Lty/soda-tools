using System;
using Autohand.Demo;
using UniRx;
using UnityEngine;

namespace Autohand
{
    /*
   * 创建者: gary
   * 功能描述:auto hand电脑端模拟器
   * 创建时间: 2022年12月05日 星期一 18:27
   */
    public class AutoHandEmulator : MonoBehaviour
    {
        [Header("启用模拟器")] 
        public bool enable = true;

        [Header("禁用AutoHandPlayer的脚本Hand Player Controller Link模拟移动")]
        public MonoBehaviour handPlayerControllerLink;

        [Header("头盔相机")] 
        public Camera hmdCamera;

        [Header("左右手控制器")] 
        public GameObject leftHand;
        public GameObject rightHand;
        
        [Header("左右手Hand脚本")]
        public Hand leftHandScript;
        public Hand rightHandScript;
        private Vector3 _lastMousePos;
        private Vector3 _hmdAngle = new Vector3();
        private Vector3 _leftHandOffset = new Vector3(-0.31f, -0.29f, 0.6f);
        private Vector3 _rightHandOffset = new Vector3(0.31f, -0.29f, 0.6f);
        [Header("玩家控制器")] 
        public AutoHandPlayer autoHandPlayer;

        [Header("按键设定")] 
        public KeyCode leftSqueezing = KeyCode.C;
        public KeyCode leftGrab = KeyCode.V;
        public KeyCode rightSqueezing = KeyCode.Z;
        public KeyCode rightGrab = KeyCode.X;
        public KeyCode leftHeldGrab = KeyCode.Q;
        public KeyCode rightHeldGrab = KeyCode.E;
        public KeyCode up = KeyCode.LeftAlt;
        public KeyCode down = KeyCode.LeftControl;

        private bool _isLeftSqueezing;
        private bool _isRightSqueezing;
        private bool _isLeftGrabbing;
        private bool _isRightGrabbing;

        [Header("传送")] 
        public Teleporter teleportScript;
        public KeyCode teleportKey = KeyCode.G;
        private bool _teleporting;

        [Header("UI交互")]
        public HandCanvasPointer uiPointer;
        public MouseButton press = MouseButton.MouseButtonLeft;
        private bool _pressed;

        [Header("远程拾取")] 
        public HandDistanceGrabber pointerGrabberRight;
        private bool _isPointering = false;

        //鼠标左键移动手
        private Vector3 _lastPressPosLeft;
        private GameObject _selectedHand;

        public enum MouseButton
        {
            MouseButtonLeft = 0,
            MouseButtonRight = 1,
            MouseButtonMiddle = 2,
        }

        private void Reset()
        {
            hmdCamera = Camera.main;
            handPlayerControllerLink = GetComponentInChildren<XRHandPlayerControllerLink>();
            var hands = GetComponentsInChildren<Hand>();
            foreach (var hand in hands)
            {
                if (hand.left)
                {
                    leftHandScript = hand;
                }
                else
                {
                    rightHandScript = hand;
                }
            }

            autoHandPlayer = GetComponentInChildren<AutoHandPlayer>();
            teleportScript = GetComponentInChildren<Teleporter>();
            uiPointer = FindObjectOfType<HandCanvasPointer>();

            var grabbers = GetComponentsInChildren<HandDistanceGrabber>();
            foreach (var grabber in grabbers)
            {
                if (grabber.primaryHand.left)
                {
                }
                else
                {
                    pointerGrabberRight = grabber;
                }
            }
        }


#if UNITY_EDITOR
        private void Start()
        {
#if UNITY_EDITOR
            if (handPlayerControllerLink) handPlayerControllerLink.enabled = false;
#endif

            Invoke(nameof(SetHeadPos), 0.2f);
        }

        void SetHeadPos()
        {
            hmdCamera.transform.localPosition = new Vector3(0, 1.5f);
        }

#endif

        private void Update()
        {
            if (!enable)
            {
                return;
            }

            Turn();
            Move();
            Lift();
            UpdateHandPose(leftHandScript, leftSqueezing, ref _isLeftSqueezing, leftGrab, ref _isLeftGrabbing,
                leftHeldGrab);
            UpdateHandPose(rightHandScript, rightSqueezing, ref _isRightSqueezing, rightGrab, ref _isRightGrabbing,
                rightHeldGrab);
            UiOperation();
            HandMove();
            Teleport();

            if (Input.GetKeyDown(KeyCode.LeftShift) && !_isPointering)
            {
                _isPointering = true;
                if (pointerGrabberRight)
                {
                    pointerGrabberRight.StartPointing();
                }
            }
            else if (Input.GetKeyUp(KeyCode.LeftShift) && _isPointering)
            {
                _isPointering = false;
                if (pointerGrabberRight)
                {
                    pointerGrabberRight.StopPointing();
                }
            }
            if (_isPointering)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    pointerGrabberRight.SelectTarget();
                }
            }
        }

        private void Teleport()
        {
            if (!_teleporting && Input.GetKeyDown(teleportKey))
            {
                _teleporting = true;
                teleportScript.StartTeleport();
            }
            else if (_teleporting && Input.GetKeyUp(teleportKey))
            {
                _teleporting = false;
                teleportScript.Teleport();
            }
        }

        private void HandMove()
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2))
            {
                _lastPressPosLeft = Input.mousePosition;
            }

            if (Input.GetMouseButton(0))
            {
                var pos = Input.mousePosition;
                var delta = pos - _lastPressPosLeft;
                _lastPressPosLeft = pos;
                if (delta.sqrMagnitude > 0.1f && _selectedHand)
                {
                    delta *= 0.002f;
                    var fwd = hmdCamera.transform.forward;
                    fwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
                    var right = hmdCamera.transform.right;
                    delta = delta.x * right + delta.y * fwd;
                    _selectedHand.transform.position += delta;
                }
            }
            else if (Input.GetMouseButton(2))
            {
                var pos = Input.mousePosition;
                var delta = pos - _lastPressPosLeft;
                _lastPressPosLeft = pos;
                if (delta.sqrMagnitude > 0.1f && _selectedHand)
                {
                    delta *= 0.002f;
                    var right = hmdCamera.transform.right;
                    _selectedHand.transform.position += (delta.y * Vector3.up + delta.x * right);
                }
            }

            if (_selectedHand)
            {
                if (Input.GetKey(KeyCode.R))
                {
                    var fwd = hmdCamera.transform.forward;
                    _selectedHand.transform.Rotate(fwd, 2);
                }
                else if (Input.GetKey(KeyCode.F))
                {
                    var fwd = hmdCamera.transform.forward;
                    _selectedHand.transform.Rotate(fwd, -2);
                }
            }


            if (Input.GetMouseButtonUp(1))
            {
                _selectedHand = null;
            }
        }

        private void Lift()
        {
            if (Input.GetKey(down))
            {
                hmdCamera.transform.position += Vector3.down * Time.deltaTime;
            }
            else if (Input.GetKey(up))
            {
                hmdCamera.transform.position += Vector3.up * Time.deltaTime;
            }
        }

        private void Move()
        {
            if (Input.GetKey(KeyCode.W))
            {
                autoHandPlayer.Move(Vector2.up);
            }
            else if (Input.GetKey(KeyCode.S))
            {
                autoHandPlayer.Move(Vector2.down);
            }

            else if (Input.GetKey(KeyCode.A))
            {
                autoHandPlayer.Move(Vector2.left);
            }
            else if (Input.GetKey(KeyCode.D))
            {
                autoHandPlayer.Move(Vector2.right);
            }
            else
            {
                autoHandPlayer.Move(Vector2.zero);
            }
        }

        private void Turn()
        {
            var lastMousePos = Input.mousePosition;
            if (Input.GetMouseButtonDown(1))
            {
                _lastMousePos = lastMousePos;
            }

            if (Input.GetMouseButton(1))
            {
                var deltaPos = lastMousePos - _lastMousePos;
                if (deltaPos.sqrMagnitude > 0.1f)
                {
                    _hmdAngle.x -= deltaPos.y * 0.2f;
                    _hmdAngle.x = Mathf.Clamp(_hmdAngle.x, -90, 90);
                    _hmdAngle.y += deltaPos.x * 0.2f;

                    hmdCamera.transform.localEulerAngles = _hmdAngle;
                }

                var handAngle = _hmdAngle;
                handAngle.x -= 20;
                leftHand.transform.localEulerAngles = handAngle;
                rightHand.transform.localEulerAngles = handAngle;
                leftHand.transform.position = hmdCamera.transform.TransformPoint(_leftHandOffset);
                rightHand.transform.position = hmdCamera.transform.TransformPoint(_rightHandOffset);
            }

            _lastMousePos = lastMousePos;
        }

        void UpdateHandPose(Hand hand, KeyCode squeezing, ref bool isSqueezing, KeyCode grab, ref bool isGrabbing,
            KeyCode held)
        {
            if (Input.GetKeyDown(squeezing) && !isSqueezing)
            {
                isSqueezing = true;
                hand.Squeeze();
            }
            else if (Input.GetKeyUp(squeezing) && isSqueezing)
            {
                isSqueezing = false;
                hand.Unsqueeze();
            }

            if (Input.GetKeyDown(grab) && !isGrabbing)
            {
                isGrabbing = true;
                hand.Grab();
            }
            else if (Input.GetKeyUp(grab) && isGrabbing)
            {
                isGrabbing = false;
                hand.Release();
            }
            

            if (Input.GetKeyDown(held))
            {
                
                if (hand.GetHeld())
                {
                    hand.Release();
                }
                else
                {
                    hand.Grab();
                }
            }
        }

        /// <summary>
        /// ui 操作
        /// </summary>
        void UiOperation()
        {
            if (_pressed && Input.GetMouseButtonUp((int) press))
            {
                _pressed = false;
                uiPointer.Release();
            }
            else if (!_pressed && Input.GetMouseButtonDown((int) press))
            {
                _pressed = true;
                uiPointer.Press();
            }
        }
    }

}