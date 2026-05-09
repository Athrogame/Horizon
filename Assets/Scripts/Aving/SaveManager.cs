using System.IO;
using UnityEngine;

public static class SaveManager
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "save.dat");

    public static void Save(SaveData data)
    {
        string json = JsonUtility.ToJson(data);
        string encrypted = EncryptionHelper.Encrypt(json);
        File.WriteAllText(SavePath, encrypted);
    }

    public static SaveData Load()
    {
        SaveData data;
        if (!File.Exists(SavePath))
        {
            data = new SaveData();
            data.ApplyDefaultsIfFirstTime();
            Save(data);
            return data;
        }

        string encrypted = File.ReadAllText(SavePath);
        string json = EncryptionHelper.Decrypt(encrypted);
        data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();

        data.EnsureListSizes();
        data.ApplyDefaultsIfFirstTime();
        Save(data);
        return data;
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }
    }

    public static void ResetToDefaults()
    {
        DeleteSave();
        SaveData data = Load();
        GameManager.I.data = data;
        PackManager.I.ownedEasy = data.unlockedPacksEasy;
        PackManager.I.ownedMedium = data.unlockedPacksMedium;
        PackManager.I.ownedHard = data.unlockedPacksHard;
    }
}