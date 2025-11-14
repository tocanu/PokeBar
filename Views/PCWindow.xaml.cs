using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PokeBar.Models;
using PokeBar.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace PokeBar.Views
{
    public partial class PCWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly GameState _state;

        public PCWindow(MainViewModel vm, GameState state)
        {
            InitializeComponent();
            _vm = vm;
            _state = state;
            LoadPokemon();
        }

        private void LoadPokemon()
        {
            // Pokémon Ativo
            var active = _state.Active;
            if (active != null)
            {
                ActiveListBox.ItemsSource = new[] { active };
            }

            // Box
            BoxListBox.ItemsSource = _state.Box.ToList();
            BoxCountText.Text = _state.Box.Count.ToString();
        }

        private void BoxListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SwitchButton.IsEnabled = BoxListBox.SelectedItem != null;
            ReleaseButton.IsEnabled = BoxListBox.SelectedItem != null;
        }

        private void SwitchButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPokemon = BoxListBox.SelectedItem as Pokemon;
            if (selectedPokemon == null) return;

            var result = MessageBox.Show(
                $"Trocar {_state.Active?.Name} por {selectedPokemon.Name}?",
                "Confirmar Troca",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                _vm.SwitchActivePokemon(selectedPokemon);
                LoadPokemon();
                MessageBox.Show($"{selectedPokemon.Name} agora é seu Pokémon ativo!", "Troca Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ReleaseButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPokemon = BoxListBox.SelectedItem as Pokemon;
            if (selectedPokemon == null) return;

            var result = MessageBox.Show(
                $"Tem certeza que deseja soltar {selectedPokemon.Name}?\nEsta ação não pode ser desfeita!",
                "Confirmar Soltar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                _vm.ReleasePokemon(selectedPokemon);
                LoadPokemon();
                MessageBox.Show($"{selectedPokemon.Name} foi solto!", "Pokémon Solto", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
