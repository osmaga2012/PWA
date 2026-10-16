namespace PWA.Models
{
    public class AppSettings
    {
        public ApiSettings ApiSettings { get; set; } = new();
        public string ApplicationName { get; set; } = "CRM PWA";
        public string Version { get; set; } = "1.0.0";
        public Features Features { get; set; } = new();
    }

    public class ApiSettings
    {
        public string BaseUrl { get; set; } = "";
        public int Timeout { get; set; } = 30;
    }

    public class Features
    {
        public bool EnableOfflineMode { get; set; }
        public bool EnablePushNotifications { get; set; }
    }
}