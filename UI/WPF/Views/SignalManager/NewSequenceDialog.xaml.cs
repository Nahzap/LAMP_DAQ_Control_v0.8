using System;
using System.Windows;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.Views.SignalManager
{
    public partial class NewSequenceDialog : Window
    {
        public string SequenceName { get; private set; }
        public double DurationSeconds { get; private set; }
        public string Description { get; private set; }

        public NewSequenceDialog()
        {
            InitializeComponent();
            SequenceNameTextBox.Focus();
            SequenceNameTextBox.SelectAll();
        }

        private void OnCreate(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[NEW SEQUENCE] Create button clicked");
            
            // Validate name
            if (string.IsNullOrWhiteSpace(SequenceNameTextBox.Text))
            {
                Console.WriteLine("[NEW SEQUENCE ERROR] Empty sequence name");
                MessageBox.Show("Please enter a sequence name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                SequenceNameTextBox.Focus();
                return;
            }

            // Validate duration
            if (!double.TryParse(DurationTextBox.Text, out double duration) || duration <= 0)
            {
                Console.WriteLine($"[NEW SEQUENCE ERROR] Invalid duration: {DurationTextBox.Text}");
                MessageBox.Show("Please enter a valid duration (positive number).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                DurationTextBox.Focus();
                return;
            }

            SequenceName = SequenceNameTextBox.Text.Trim();
            DurationSeconds = duration;
            Description = DescriptionTextBox.Text.Trim();

            Console.WriteLine($"[NEW SEQUENCE] Created: Name={SequenceName}, Duration={DurationSeconds}s, Description={Description}");

            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[NEW SEQUENCE] Cancelled");
            DialogResult = false;
            Close();
        }
    }
}
