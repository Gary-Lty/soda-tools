namespace Gary.UIManagement
{
    public class Singleton<T> where T : class,new()
    {
        /*	Instance	*/
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new();
                }

                return _instance;
            }
        }

        

        /*	Destroy	*/
        public static void Destroy()
        {
            _instance = null;
        }
    }
}