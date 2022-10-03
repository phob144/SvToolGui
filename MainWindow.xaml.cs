using OsuParsers.Decoders;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;
using Microsoft.Win32;
using System.Linq;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using System.Globalization;

namespace SvToolGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void browseButton_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new OpenFileDialog()
            {
                Filter = "Beatmap Files|*.osu",
                Multiselect = true
            };

            var result = fileDialog.ShowDialog();

            // null/false = error/action cancelled
            if (result != true)
                return;

            if (fileDialog.FileNames.Any(x => Path.GetExtension(x) != ".osu"))
            {
                MessageBox.Show("You chose invalid files", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            beatmapsTextBox.Text = string.Join('|', fileDialog.FileNames);
        }

        private void applyChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(beatmapsTextBox.Text) ||
                string.IsNullOrEmpty(startTimeTextBox.Text) ||
                string.IsNullOrEmpty(endTimeTextBox.Text) ||
                string.IsNullOrEmpty(multiplierTextBox.Text))
            {
                MessageBox.Show("You didn't fill every box", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var startTime = ParseTimestamp(startTimeTextBox.Text);
            var endTime = ParseTimestamp(endTimeTextBox.Text);

            var isMultiplierValid = double.TryParse(multiplierTextBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var multiplier);

            if (startTime == null ||
                endTime == null ||
                !isMultiplierValid)
            {
                MessageBox.Show("Your input is invalid", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var paths = beatmapsTextBox.Text.Split('|');

            foreach (var path in paths)
            {
                if (!File.Exists(path))
                    continue;

                var beatmap = BeatmapDecoder.Decode(path);

                foreach (var timingPoint in beatmap.TimingPoints)
                {
                    if (!timingPoint.Inherited)
                        continue;

                    // in the .osu file format, sv is written as -100 / actual_sv, so the lower it is, the faster u go
                    if (timingPoint.Offset >= startTime && timingPoint.Offset < endTime)
                        timingPoint.BeatLength /= multiplier;
                }

                if (separateOutputCheckBox.IsChecked ?? false)
                {
                    beatmap.MetadataSection.Version += " (SV EDIT)";

                    string outputFileName = $"{beatmap.MetadataSection.Artist} - {beatmap.MetadataSection.Title} ({beatmap.MetadataSection.Creator}) [{beatmap.MetadataSection.Version}].osu";

                    // replace illegal characters with an empty string like osu does, taken from https://stackoverflow.com/questions/146134/how-to-remove-illegal-characters-from-path-and-filenames
                    outputFileName = string.Join("", outputFileName.Split(Path.GetInvalidFileNameChars()));

                    beatmap.Save($@"{Path.GetDirectoryName(path)}\{outputFileName}");
                }
                else
                {
                    beatmap.Save(path);
                }
            }

            MessageBox.Show("Done!");
        }

        private static int? ParseTimestamp(string timestamp)
        {
            // unfortunately, i must trust hivie with this
            var regex = new Regex(@"(\d+):(\d{2}):(\d{3})");
            var matches = regex.Matches(timestamp);

            if (matches.Count == 0)
                return null;

            var groups = matches[0].Groups;

            // [1] = min, [2] = sec, [3] = ms
            return int.Parse(groups[1].Value) * 60000 + int.Parse(groups[2].Value) * 1000 + int.Parse(groups[3].Value);
        }
    }
}
