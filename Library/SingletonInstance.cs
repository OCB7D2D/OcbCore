// Tired of writing this over and over again ;)
public class SingletonInstance<T> : object where T : new()
{
    protected static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new T();
            }
            return instance;
        }
    }
    public static bool HasInstance()
    {
        return instance != null;
    }
}
