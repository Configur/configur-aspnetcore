namespace Configur.AspNetCore
{
    public class FindAppSettingsProjection
    {
        public string Ciphertext { get; set; }
        public string ETag { get; set; }
        public string PrivateKeyCiphertext { get; set; }
        public SignalR SignalR { get; set; }
    }
}
