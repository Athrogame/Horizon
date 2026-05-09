using System.Collections.Generic;

[System.Serializable]
public class SaveData //this is the script that will be referenced in your main game's script as a variable:
    //public SaveData data; - go to the bottom of this script to see how other scripts can properly Save() and Load() data.
{

    //the data I save from my game
    public const int PackSlots = 50;
    public List<bool> unlockedPacksEasy = new List<bool>(new bool[PackSlots]);
    public List<bool> unlockedPacksMedium = new List<bool>(new bool[PackSlots]);
    public List<bool> unlockedPacksHard = new List<bool>(new bool[PackSlots]);
    public List<int> highScore = new List<int>(new int[PackSlots]);
    public List<int> timesPlayed = new List<int>(new int[PackSlots]);
    public int currentPack;
    public int currentDifficulty;
    public int currentLevel;
    public float balance;
    public bool firstTimePlaying = true;
    public bool hasData = false;
    public float totalBal;

    ///
    public const float DefaultMusicVolume = 0.1f;//not sure where const came from, here
    public const float DefaultSFXVolume = 1f;
    public const float DefaultCameraVFX = 0.15f;

    public float MusicVolume = DefaultMusicVolume;
    public float SFXVolume = DefaultSFXVolume;
    public float CameraVFX = DefaultCameraVFX;
    public bool BGToggle = true;
    public bool VFXToggle = true;
    public bool AdToggle = true;
    public bool ChatterToggle = true;
    public void ApplyDefaultsIfFirstTime()
    {
        EnsureListSizes();
        if (hasData) return; //this is a "first time opening game" var, never triggers again if not first time.
        MusicVolume = DefaultMusicVolume;
        SFXVolume = DefaultSFXVolume;
        CameraVFX = DefaultCameraVFX;
        BGToggle = true;
        VFXToggle = true;
        AdToggle = true;
        ChatterToggle = true;
        hasData = true;
        firstTimePlaying = true;
        GameManager.I.firstTimePlaying = true;
        unlockedPacksEasy[0] = true;
    }

    public void EnsureListSizes() //future proofing my unlocked courses lists.
    {
        if (unlockedPacksEasy == null) unlockedPacksEasy = new List<bool>();
        if (unlockedPacksMedium == null) unlockedPacksMedium = new List<bool>();
        if (unlockedPacksHard == null) unlockedPacksHard = new List<bool>();
        if (highScore == null) highScore = new List<int>();
        if (timesPlayed == null) timesPlayed = new List<int>();

        while (unlockedPacksEasy.Count < PackSlots) unlockedPacksEasy.Add(false);
        while (unlockedPacksMedium.Count < PackSlots) unlockedPacksMedium.Add(false);
        while (unlockedPacksHard.Count < PackSlots) unlockedPacksHard.Add(false);
        while (highScore.Count < PackSlots) highScore.Add(0);
        while (timesPlayed.Count < PackSlots) timesPlayed.Add(0);
    }


    //these are snippets from my GameManager.cs file.
    //(how to save)
    //
    // SaveData data = SaveManager.Load();      //data needs to be up-to date before saving, could be cut out at times if needed
    // data.currentLevel = 0;                   //pulled from when player gameovers, i'm resetting their curr level & balance.
    // data.balance = 0;
    // SaveManager.Save(data);                  //this line saves the "data" variable (with new numbers) to the encrypted json. 



    //(how to load)
    //
    // SaveData data = SaveManager.Load();      //pull data from encrypted file
    // level = data.currentLevel;               //set the actual variables to the data's save
    // balance = data.balance;                  
    // firstTimePlaying = data.firstTimePlaying;
}