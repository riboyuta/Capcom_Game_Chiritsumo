using UnityEngine;

public static class AudioEvent
{
    public static void Emit(Component source, string eventName)
    {
        if (!TryGetBinder(source, out AudioEventBinder binder))
        {
            return;
        }

        binder.Emit(eventName);
    }

    public static void Emit(GameObject source, string eventName)
    {
        if (!TryGetBinder(source, out AudioEventBinder binder))
        {
            return;
        }

        binder.Emit(eventName);
    }

    public static void EmitAt(Component source, string eventName, Vector3 position)
    {
        if (!TryGetBinder(source, out AudioEventBinder binder))
        {
            return;
        }

        binder.EmitAt(eventName, position);
    }

    public static void EmitAt(GameObject source, string eventName, Vector3 position)
    {
        if (!TryGetBinder(source, out AudioEventBinder binder))
        {
            return;
        }

        binder.EmitAt(eventName, position);
    }

    private static bool TryGetBinder(Component source, out AudioEventBinder binder)
    {
        binder = null;

        if (source == null)
        {
            return false;
        }

        binder = source.GetComponent<AudioEventBinder>();
        return binder != null;
    }

    private static bool TryGetBinder(GameObject source, out AudioEventBinder binder)
    {
        binder = null;

        if (source == null)
        {
            return false;
        }

        binder = source.GetComponent<AudioEventBinder>();
        return binder != null;
    }
}
