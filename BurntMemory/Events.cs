namespace BurntMemory
{
    public class Events
    {



        public static event EventHandler? EXTERNAL_PROCESS_CLOSED_EVENT;

        public static void EXTERNAL_PROCESS_CLOSED_EVENT_INVOKE(object? sender, EventArgs e)
        {
            EventHandler? handler = EXTERNAL_PROCESS_CLOSED_EVENT;
            if (handler != null)
            {
                handler(sender, e);
            }
        }



        public class AttachedEventArgs : EventArgs 
        {
            public string? NameOfProcess;
            public string? ProcessVersion;
        }



        public static event EventHandler<AttachedEventArgs>? ATTACH_EVENT;

        public static void ATTACH_EVENT_INVOKE(object? sender, AttachedEventArgs e)
        {
            EventHandler<AttachedEventArgs>? handler = ATTACH_EVENT;
            if (handler != null)
            {
                handler(sender, e);
            }
        }

        public static event EventHandler? DEATTACH_EVENT;

        public static void DEATTACH_EVENT_INVOKE(object? sender, EventArgs e)
        {
            EventHandler? handler = DEATTACH_EVENT;
            if (handler != null)
            {
                handler(sender, e);
            }
        }

        public static event EventHandler? DLL_LOAD_EVENT;

        public static void DLL_LOAD_EVENT_INVOKE(object? sender, EventArgs e)
        {
            EventHandler? handler = DLL_LOAD_EVENT;
            if (handler != null)
            {
                handler(sender, e);
            }
        }

        public static event EventHandler? DLL_UNLOAD_EVENT;

        public static void DLL_UNLOAD_EVENT_INVOKE(object? sender, EventArgs e)
        {
            EventHandler? handler = DLL_UNLOAD_EVENT;
            if (handler != null)
            {
                handler(sender, e);
            }
        }

        public static event EventHandler? THREAD_LOAD_EVENT;

        public static void THREAD_LOAD_EVENT_INVOKE(object? sender, EventArgs e)
        {
            EventHandler? handler = THREAD_LOAD_EVENT;
            if (handler != null)
            {
                handler(sender, e);
            }
        }

        public static event EventHandler? THREAD_UNLOAD_EVENT;

        public static void THREAD_UNLOAD_EVENT_INVOKE(object? sender, EventArgs e)
        {
            EventHandler? handler = THREAD_UNLOAD_EVENT;
            if (handler != null)
            {
                handler(sender, e);
            }
        }
    }
}