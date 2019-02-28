﻿using System;
using UnityEngine;
using System.Collections.Generic;
using MagiCloud.Core.MInput;
using MagiCloud.Core.Events;
using MagiCloud.Core;
using MagiCloud.Features;

namespace MagiCloud.Operate
{
    /// <summary>
    /// 鼠标控制端
    /// </summary>
    public class MouseController : MonoBehaviour, IHandController
    {
        private MBehaviour behaviour;

        private bool IsZoom = false; //开启缩放
        private bool IsRotate = false; //开启旋转

        private bool IsRotateDown = false; //旋转是否开启

        [Header("手图标")]
        public HandIcon handSprite;//手图标

        [Header("图标大小")]
        public Vector2 handSize;//图标大小

        private bool isPlaying = false;

        private IOperateObject operateObject;

        private Vector3 offset;
        private bool isEnable;
        private MOperate operate;

        /// <summary>
        /// 输入端
        /// </summary>
        public Dictionary<int, MInputHand> InputHands {
            get;set;
        }

        /// <summary>
        /// 是否启动
        /// </summary>
        public bool IsPlaying {
            get {
                return isPlaying;
            }
        }

        public bool IsEnable {
            get {
                return isEnable;
            }
            set {

                if (isEnable == value) return;
                isEnable = value;

                if (isEnable)
                {
                    behaviour = new MBehaviour(ExecutionPriority.Highest, -900);
                    behaviour.OnUpdate_MBehaviour(OnMouseUpdate);

                    enabled = true;
                    operate.OnEnable();

                    //开启事件发送
                    //EventHandStart.SendListener(0);
                }
                else
                {
                    
                    enabled = false;
                    operate.OnDisable();

                    //停止事件发送
                    //EventHandStop.SendListener(0);

                    //InputHands[0].SetIdle(); //停止后，设置为Idle状态
                }
            }
        }

        /// <summary>
        /// 获取到指定输入端对象
        /// </summary>
        /// <param name="handIndex"></param>
        /// <returns></returns>
        public MInputHand GetInputHand(int handIndex)
        {
            MInputHand hand;

            InputHands.TryGetValue(handIndex, out hand);

            if (hand == null)
                throw new Exception("手势编号错误：" + handIndex);

            return hand;
        }

        private void Awake()
        {
            behaviour = new MBehaviour(ExecutionPriority.Highest, -900, enabled);

            InputHands = new Dictionary<int, MInputHand>();

            //初始化手的种类
            var handUI = MHandUIManager.CreateHandUI(transform, handSprite, handSize);
            var inputHand = new MInputHand(0, handUI, OperatePlatform.Mouse);
            handUI.name = "Mouse-Hand";

            InputHands.Add(0, inputHand);

            isPlaying = true;

            //注册操作者相关事件
            operate = MOperateManager.AddOperateHand(inputHand, this);
            //注册方法
            operate.OnGrab = OnGrabObject;
            operate.OnSetGrab = SetGrabObject;

            IsEnable = true;
        }

        /// <summary>
        /// 鼠标控制端的妹帧执行
        /// </summary>
        void OnMouseUpdate()
        {
            if (!IsEnable) return;

            //将他的屏幕坐标传递出去
            InputHands[0].OnUpdate(Input.mousePosition);

            if (Input.GetMouseButtonDown(0)&&InputHands[0].IsIdleStatus)
                InputHands[0].SetGrip();

            if (Input.GetMouseButtonUp(0) && !(InputHands[0].IsRotateZoomStatus || InputHands[0].IsErrorStatus))
                InputHands[0].SetIdle();

            #region 旋转

            //如果按下右键
            if (Input.GetMouseButtonDown(1))
            {
                IsRotateDown = true;
                IsRotate = false;
            }

            if (Input.GetMouseButtonUp(1))
            {
                IsRotateDown = false;

                //已经存在旋转，并且在集合中记录
                if (IsRotate)
                {
                    EventCameraRotate.SendListener(Vector3.zero);
                }

                IsRotate = false;
                InputHands[0].HandStatus = MInputHandStatus.Idle;
            }

            //按住右键旋转
            if (IsRotateDown)
            {
                //向量的模大于2.0时
                if (!IsRotate && InputHands[0].IsIdleStatus && InputHands[0].ScreenVector.magnitude > 2.0f)
                {
                    //将动作记录到集合中
                    InputHands[0].HandStatus = MInputHandStatus.Rotate;

                    IsRotate = true;
                }

                //已经存在旋转，并且在集合中记录
                if (IsRotate)
                {
                    EventCameraRotate.SendListener(InputHands[0].ScreenVector);
                }

            }
            #endregion

            #region 缩放

            //缩放
            if (Input.GetAxis("Mouse ScrollWheel") != 0)
            {
                float result = Input.GetAxis("Mouse ScrollWheel");

                if (!IsZoom && InputHands[0].IsIdleStatus)
                {

                    InputHands[0].HandStatus = MInputHandStatus.Zoom;
                    IsZoom = true;
                }

                //进行缩放
                if (IsZoom)
                {
                    EventCameraZoom.SendListener(result);
                }
            }
            else
            {
                //进行缩放
                if (IsZoom)
                {
                    EventCameraZoom.SendListener(0);
                    IsZoom = false;
                    InputHands[0].HandStatus = MInputHandStatus.Idle;
                }



            }
            #endregion

            if (operateObject != null)
            {
                switch (InputHands[0].HandStatus)
                {
                    case MInputHandStatus.Grabing:

                        //需要处理偏移量
                        var screenDevice = MUtility.MainWorldToScreenPoint(operateObject.GrabObject.transform.position);
                        Vector3 screenMouse = InputHands[0].ScreenPoint;
                        Vector3 vPos = MUtility.MainScreenToWorldPoint(new Vector3(screenMouse.x, screenMouse.y, screenDevice.z));

                        Vector3 position = vPos - offset;

                        EventUpdateObject.SendListener(operateObject.GrabObject, position, operateObject.GrabObject.transform.rotation, InputHands[0].HandIndex);

                        break;
                    case MInputHandStatus.Idle:

                        this.operateObject = null;
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// 移动设置
        /// </summary>
        /// <param name="operate"></param>
        /// <param name="handIndex"></param>
        void OnGrabObject(IOperateObject operate, int handIndex)
        {
            if (handIndex != InputHands[0].HandIndex) return;

            offset = MUtility.GetOffsetPosition(InputHands[0].ScreenPoint, operate.GrabObject);

            this.operateObject = operate;
        }

        /// <summary>
        /// 设置物体被抓取
        /// </summary>
        /// <param name="operate"></param>
        /// <param name="handIndex"></param>
        /// <param name="cameraRelativeDistance"></param>
        void SetGrabObject(IOperateObject operate, int handIndex, float cameraRelativeDistance)
        {
            if (handIndex != InputHands[0].HandIndex) return;

            //Vector3 screenDevice = MUtility.MainWorldToScreenPoint(operate.GrabObject.transform.position);
            Vector3 screenpoint = InputHands[0].ScreenPoint;
            operateObject = operate;

            Vector3 screenMainCamera = MUtility.MainWorldToScreenPoint(MUtility.MainCamera.transform.position 
                + MUtility.MainCamera.transform.forward * cameraRelativeDistance);

            Vector3 position = MUtility.MainScreenToWorldPoint(new Vector3(screenpoint.x, screenpoint.y, screenMainCamera.z));

            offset = Vector3.zero;

            operateObject.GrabObject.transform.position = position;
        }

        private void OnDestroy()
        {
            behaviour.OnExcuteDestroy();
        }

        public void StartOnlyHand()
        {
            //不做任何处理
        }

        public void StartMultipleHand()
        {
            //不用做任何处理
        }
    }
}
