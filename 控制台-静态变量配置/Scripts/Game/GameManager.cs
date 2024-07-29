using System;
using UnityEngine;

namespace Game
{
    public class GameManager : MonoBehaviour
    {
        private void Awake()
        {
            ConfigVar.Init();
            Console.Init();
        }

        private void Update()
        {
            Console.ConsoleUpdate();
        }
    }
}