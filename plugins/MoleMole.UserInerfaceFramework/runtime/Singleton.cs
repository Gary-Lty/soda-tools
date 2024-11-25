using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/*
 *	
 *  Singleton
 *
 *	by Xuanyi
 *
 */

namespace MoleMole
{
    public class Singleton<T> where T : class
    {
        /*	Instance	*/
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    Create();
                }

                return _instance;
            }
        }


        public static void Create()
        {
            _instance = (T)Activator.CreateInstance(typeof(T), true);
        }
        
        
        /* Static constructor	*/
        static Singleton()
        {
            return;
        }

        /*	Destroy	*/
        public static void Destroy()
        {

            _instance = null;

            return;
        }
    }
}
