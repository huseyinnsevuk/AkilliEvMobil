using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    public partial class AutomationPage : ContentPage
    {
        public ObservableCollection<Routine> Routines { get; set; }
        public ObservableCollection<Routine> FilteredRoutines { get; set; }

        public AutomationPage()
        {
            InitializeComponent();
            
            Routines = new ObservableCollection<Routine>
            {
                new Routine { Name = "Karşılama Modu", Description = "Siz eve gelmeden konfor şartlarını hazırlar.", Icon = "home_on.png" },
                new Routine { Name = "Tasarruf Modu", Description = "Enerji tüketimini minimuma indirir.", Icon = "power.png" },
                new Routine { Name = "Tatil Modu", Description = "Güvenliği maksimize eder ve varlık simülasyonu yapar.", Icon = "lock.png" }
            };

            FilteredRoutines = new ObservableCollection<Routine>(Routines);
            BindingContext = this;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = e.NewTextValue?.ToLower() ?? "";
            var filtered = Routines.Where(r => 
                r.Name.ToLower().Contains(searchText) || 
                r.Description.ToLower().Contains(searchText)
            ).ToList();

            FilteredRoutines.Clear();
            foreach (var routine in filtered)
            {
                FilteredRoutines.Add(routine);
            }
        }

        private async void OnRoutineTapped(object sender, EventArgs e)
        {
            if (sender is View view && view.BindingContext is Routine routine)
            {
                // Pop animation
                await view.ScaleTo(0.95, 100);
                await view.ScaleTo(1.0, 100);

                await DisplayAlert("Rutin Başlatıldı", $"{routine.Name} senaryosu başarıyla çalıştırıldı.", "Tamam");
            }
        }
    }

    public class Routine
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
    }
}
