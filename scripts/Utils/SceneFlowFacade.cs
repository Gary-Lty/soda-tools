using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 托管 awake 和 start
/// </summary>
public class SceneFlowFacade : MonoBehaviour
{
    public List<GameObject> executionOrderList;
    private HashSet<IHasFlow> _flowSetList = new();
    private List<IHasFlow> _flowList;

    private void Awake()
    {
        foreach (var go in executionOrderList)
        {
            var arr = go.GetComponents<IHasFlow>();
            _flowSetList.UnionWith(arr);
        }

        this._flowList = _flowSetList.OrderBy(e => e.ExecutionPriority).ToList();
        foreach (var flow in _flowList)
        {
            try
            {
                flow.OnAwake();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    private void Start()
    {
        foreach (var flow in _flowList)
        {
            try
            {
                flow.OnStart();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}

/// <summary>
/// 托管 start 和 awake
/// </summary>
public interface IHasFlow
{
    int ExecutionPriority { get; }
    void OnAwake();
    void OnStart();
}