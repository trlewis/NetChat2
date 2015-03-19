namespace NetChat2Client
{
    public static class SystemHelper
    {
        public static string GetCurrentUserName()
        {
            var current = System.Security.Principal.WindowsIdentity.GetCurrent();
            if (current == null)
            {
                return "somebody";
            }

            var index = current.Name.LastIndexOf('\\');
            return index > 0 ? current.Name.Substring(index + 1) : current.Name;
        }
    }
}