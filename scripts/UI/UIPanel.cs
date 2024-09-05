using Game.Manager;
using UnityEngine;

namespace UI
{
    public class UIPanel : MonoBehaviour
    {
        [SerializeField] private PanelType type;
        public PanelType PanelType => type;


        public virtual void TryOpen()
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }
        }

        public virtual void TryOpen(PanelData data)
        {
            TryOpen();
        }

        public virtual void TryClose()
        {
            if (gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
            }
        }
    }

    public class PanelData
    {
    }
}