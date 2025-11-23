// Path: Assets/Game/Scripts/AccountManager.cs

using UnityEngine;
using System.Threading.Tasks;
using Firebase.Auth;

public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance;
    private string deviceToken;  // Cihaz için unique token
    private string accountId;    // Hesap için unique ID
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            deviceToken = SystemInfo.deviceUniqueIdentifier;
            LoadAccount();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadAccount()
    {
        accountId = PlayerPrefs.GetString("AccountId", "");
    }

    public async Task<string> GetOrCreateAccount()
    {
        if (string.IsNullOrEmpty(accountId))
        {
            // Yeni hesap oluştur
            var authResult = await FirebaseAuth.DefaultInstance.SignInAnonymouslyAsync();
            accountId = authResult.User.UserId;
            PlayerPrefs.SetString("AccountId", accountId);
            PlayerPrefs.Save();
            
            // Device token'ı kaydet
            await FirebaseManager.Instance.SaveDeviceToken(accountId, deviceToken);
        }
        return accountId;
    }
}