using System.Collections.Generic;
using Game.Data;
using Game.Message;
using HCR.Manager;
using UI;
using UniRx;
using UnityEngine;
using Utils;

namespace Game.Manager
{
    public enum PanelType
    {
        None = -1,
        Init = 0,
        Start = 1,
        SelectRole = 2,
        Setting = 3,
        InfoShow = 4,
        Decal = 5,
        NewRecord = 6,
        BillboardRank = 7, //比赛结束后的排行榜，显示本次比赛的排名
        Const = 8, //常驻
        UserHall = 9, //名人堂
        GiftEffect = 10, //送礼动画

        WorldRankQuick = 11,
        WorldRankSlow = 12,

        MonthRank = 13,
        WeekRank = 14,
        WinRank = 15,
        FamousRank = 16,
    }

    public class UIManager : MonoSingleton<UIManager>
    {
        [SerializeField] public List<UIPanel> panelList;
        private Dictionary<PanelType, UIPanel> _panelDict = new();
        [Header("渲染层级")] [SerializeField] private List<Transform> layerList = new();

        [Header("常驻面板")] [SerializeField] private PanelConst panelConst;

        [Header("礼物动画")] [SerializeField] private GameObject popPageGift;

        [Header("名人堂")] [SerializeField] private GameObject popFamousHill;
        private Queue<User> _waitForShowUserQueue = new();
        private IGameData _gameData;
        private List<UIPanel> _openPanelList = new();

        protected override void Awake()
        {
            base.Awake();
            _panelDict.Clear();
            foreach (var panel in panelList)
            {
                if (panel)
                {
                    _panelDict.Add(panel.PanelType, panel);
                    panel.gameObject.SetActive(false);
                }
            }
            this.Open(PanelType.Setting);
        }

        private void Start()
        {
            _gameData = MainFramework.Interface.GetModel<IGameData>();
            MessageBroker.Default.Receive<UserDataUpdateEvt>().Subscribe(OnUserJoin).AddTo(this);
            ObservableExtensions.Subscribe(MessageBroker.Default.Receive<AppGiftMessage>(), PlayGiftEffectOnGetGift)
                .AddTo(this);
        }

        bool TryGetLastPanel(out UIPanel panel)
        {
            if (_openPanelList.Count > 0)
            {
                panel = _openPanelList[^1];
                return true;
            }

            panel = null;
            return false;
        }

        public void Close(PanelType type)
        {
            if (_panelDict.TryGetValue(type, out var panel))
            {
                panel.TryClose();
                _openPanelList.Remove(panel);
                
                if (TryGetLastPanel(out var lastPanel))
                {
                    lastPanel.TryOpen();
                }
            }
        }

        /// <summary>
        /// 关闭并打开上一个panel
        /// </summary>
        public void Close()
        {
            if (TryGetLastPanel(out var panel))
            {
                Close(panel.PanelType);
            }
        }

        /// <summary>
        /// 打开panel，并关闭上一个panel
        /// </summary>
        /// <param name="type"></param>
        /// <param name="param"></param>
        public void Open(PanelType type, PanelData param = null)
        {
            if (_panelDict.TryGetValue(type, out var panel))
            {
                if (TryGetLastPanel(out var last))
                {
                    last.TryClose();
                }
                panel.TryOpen(param);
                _openPanelList.Add(panel);
            }
            else
            {
                Debug.LogError("Panel 不存在 " + type);
            }
        }

        public void ShowCounter(int i)
        {
            panelConst.StartGameEndCounter(i);
        }
        


        #region 名人堂

        /// <summary>
        /// 尝试显示名人堂
        /// </summary>
        /// <param name="evt"></param>
        void OnUserJoin(UserDataUpdateEvt evt)
        {
            TextUtil.LogJson(evt.user);
            var isFamous = AudienceManager.IsFamous(evt.user.week_rank, evt.user.month_rank);
            TextUtil.LogJson(evt);
            if (isFamous)
            {
                if (_waitForShowUserQueue.Count <= 0)
                {
                    //没有正在显示的名人
                    PanelFamous.user = evt.user;
                    PopFamous();
                }

                _waitForShowUserQueue.Enqueue(evt.user);
            }
        }

        public void TryShowNextFamousUser()
        {
            if (_waitForShowUserQueue.TryDequeue(out var last))
            {
                if (_waitForShowUserQueue.Count > 0)
                {
                    var head = _waitForShowUserQueue.Peek();
                    PanelFamous.user = head;
                    PopFamous();
                }
            }
        }

        void PopFamous()
        {
            var root = layerList[PanelFamous.layer];
            Instantiate(popFamousHill, root);
        }

        #endregion


        void PlayGiftEffectOnGetGift(AppGiftMessage message)
        {
            if (_gameData.GameState.Value == GameState.Gaming || _gameData.GameState.Value == GameState.Ready)
            {
                var root = this.GetLayer(PanelGiftTip.layer);
                var item = Instantiate(popPageGift, root).GetComponent<PanelGiftTip>();
                item.GetComponent<PanelGiftTip>().PlayGiftEffectOnGetGift(message);
            }
        }

        private Transform GetLayer(int layer)
        {
            return layerList[layer];
        }

        /// <summary>
        /// 游戏开始前的倒计时
        /// </summary>
        public void ShowStartGameCounterDown()
        {
            panelConst.StartGameStartCounter();
        }

        public void ShowGameNearEndTip()
        {
            if (panelConst.gameObject.activeInHierarchy)
            {
                panelConst.UpdateNearEndTip();
            }
        }


        public void CloseAll()
        {
            foreach (var panel in _openPanelList)
            {
                panel.TryClose();
            }
            _openPanelList.Clear();
        }

        public void ToggleConstPanel(bool b)
        {
            if (b)
            {
                panelConst.TryOpen();

            }
            else
            {
                panelConst.TryClose();

            }
        }
    }
}