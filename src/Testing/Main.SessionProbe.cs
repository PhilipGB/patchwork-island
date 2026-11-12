using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

public partial class Main
{
    internal sealed partial class RuntimeProbe
    {
        internal void Start() => _game.StartAdventure();
        internal void Continue() => _game.ContinueAdventure();
        internal void Pause() => _game.ShowPauseMenu();
        internal void Resume() => _game.ResumeGame();
        internal void OpenSettingsFromPause() => _game.OpenSettingsFromPause();
        internal void OpenSettingsFromTitle() => _game.OpenSettingsFromTitle();
        internal void ConfirmNewGame() => _game.ConfirmNewGame();
        internal void RequestNewGameFromTitle() => _game.StartNewGameFromTitle();
        internal SaveResult StartNewGameFromTitleConfirmed() =>
            _game.BeginNewGameFromTitle(CharacterCustomisation.DefaultPreset);
        internal SaveResult StartNewGameFromTitleConfirmed(string preset) => _game.BeginNewGameFromTitle(preset);
        internal SaveResult StartNewGame(string preset) => _game.ResetSession(preset);
        internal void OpenClothesShop() => _game.VillageStations.InteractClothesShop();
        internal bool PressModalButton(string name)
        {
            if (!_game._hud.ModalOpen) return false;
            var button = _game._hud.FindChild(name, true, false) as Button;
            if (button == null || button.Disabled || !button.Visible) return false;
            button.EmitSignal(BaseButton.SignalName.Pressed);
            return true;
        }

        internal bool FocusModalButton(string name)
        {
            if (!_game._hud.ModalOpen) return false;
            var button = _game._hud.FindChild(name, true, false) as Button;
            if (button == null || button.Disabled || !button.Visible) return false;
            button.GrabFocus();
            return true;
        }

        internal string ModalLabelText(string name)
        {
            if (!_game._hud.ModalOpen) return "";
            return (_game._hud.FindChild(name, true, false) as Label)?.Text ?? "";
        }

        internal ActionOutcome ShopBuy(string outfitId)
        {
            var outcome = CharacterCustomisation.BuyOutfit(_game._data, outfitId);
            if (outcome.Success) _game.VillageStations.PersistCustomisation();
            return outcome;
        }
        internal ActionOutcome ShopWear(string outfitId)
        {
            var outcome = CharacterCustomisation.EquipOutfit(_game._data, outfitId);
            if (outcome.Success) _game.VillageStations.PersistCustomisation();
            return outcome;
        }
        internal void ReturnToTitle() => _game.ReturnToTitle();
        internal void ReturnAnyway() => _game.DiscardUnsavedSessionAndShowTitle();
        internal Error ReloadScene() => _game.GetTree().ReloadCurrentScene();
        internal void CloseModal() => _game._hud.CloseModal();
        internal bool SaveNow() => _game.SaveGame(SaveReason.Manual).Success;
        internal bool ForceAutosave() => _game.SaveGame(SaveReason.Autosave).Success;
        internal SaveResult StartNewGame() => _game.ResetSession();
        internal int Coins => _game._data.Coins;
        internal string CoinText => _game._hud.CoinText;
        internal int CompletedCount => _game._data.CompletedMissions.Count;
        internal string ActiveMission => _game._data.ActiveMission;
        internal bool MusicEnabled => _game._data.Settings.MusicEnabled;

        internal void ToggleMusic()
        {
            _game._data.Settings.MusicEnabled = !_game._data.Settings.MusicEnabled;
            _game.ApplySettings();
            _game.SaveSettings();
        }

        internal void ToggleSfx()
        {
            _game._data.Settings.SfxEnabled = !_game._data.Settings.SfxEnabled;
            _game.ApplySettings();
            _game.SaveSettings();
        }

        internal string MusicId => _game._audio?.CurrentMusicId ?? "";
        internal bool MusicPlaying => _game._audio?.IsMusicPlaying == true;
        internal bool HasMusicStream => _game._audio?.HasMusicStream == true;
        internal double MusicPosition => _game._audio?.MusicPlaybackPosition ?? 0.0;
        internal bool MusicBusMuted => AudioManager.IsBusMuted(AudioCueCatalog.MusicBus);
        internal bool SfxBusMuted => AudioManager.IsBusMuted(AudioCueCatalog.SfxBus);
        internal bool UiBusMuted => AudioManager.IsBusMuted(AudioCueCatalog.UiBus);
        internal bool VehiclesBusMuted => AudioManager.IsBusMuted(AudioCueCatalog.VehiclesBus);
        internal bool AudioBusesPresent => AudioManager.HasRequiredBuses();
        internal bool VanHasEngineAudio => _game._van?.HasEngineAudio == true;
        internal bool VanEngineIdlePlaying => _game._van?.EngineIdlePlaying == true;
        internal bool VanEngineDrivePlaying => _game._van?.EngineDrivePlaying == true;
        internal string LastSfx => _game._audio?.LastSfxId ?? "";
        internal int SfxPlayCount(string id) => _game._audio?.PlayCount(id) ?? 0;

    }
}
