using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace TaggJack.UI;

public partial class LabelResizeWindow : Window {
	private double _currentWidth;

	private double _currentHeight;














	public double NewWidth { get; private set; }

	public double NewHeight { get; private set; }

	public LabelResizeMode SelectedResizeMode { get; private set; }

	public double NewMarginLeft { get; private set; }

	public double NewMarginRight { get; private set; }

	public double NewMarginTop { get; private set; }

	public double NewMarginBottom { get; private set; }

	public LabelResizeWindow(double currentWidth, double currentHeight, double marginLeft = 2.0, double marginRight = 2.0, double marginTop = 2.0, double marginBottom = 2.0)
	{
		InitializeComponent();
		_currentWidth = currentWidth;
		_currentHeight = currentHeight;
		WidthTextBox.Text = currentWidth.ToString("F1");
		HeightTextBox.Text = currentHeight.ToString("F1");
		MarginLeftBox.Text = marginLeft.ToString("F1");
		MarginRightBox.Text = marginRight.ToString("F1");
		MarginTopBox.Text = marginTop.ToString("F1");
		MarginBottomBox.Text = marginBottom.ToString("F1");
		PresetSizeComboBox.SelectedIndex = 0;
		UpdatePreviewText();
		WidthTextBox.TextChanged += OnDimensionChanged;
		HeightTextBox.TextChanged += OnDimensionChanged;
	}

	private void PresetSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (PresetSizeComboBox.SelectedItem is ComboBoxItem { Tag: var tag } comboBoxItem && !string.IsNullOrEmpty(tag?.ToString()))
		{
			string[] array = comboBoxItem.Tag.ToString().Split(',');
			if (array.Length == 2 && double.TryParse(array[0], out var result) && double.TryParse(array[1], out var result2))
			{
				WidthTextBox.Text = result.ToString("F1");
				HeightTextBox.Text = result2.ToString("F1");
			}
		}
	}

	private void OnDimensionChanged(object sender, TextChangedEventArgs e)
	{
		UpdatePreviewText();
	}

	private void UpdatePreviewText()
	{
		if (PreviewText == null)
		{
			return;
		}
		if (double.TryParse(WidthTextBox.Text, out var result) && double.TryParse(HeightTextBox.Text, out var result2))
		{
			double value = (result - _currentWidth) / _currentWidth * 100.0;
			double value2 = (result2 - _currentHeight) / _currentHeight * 100.0;
			string value3 = "";
			if (Math.Abs(value) > 0.1 || Math.Abs(value2) > 0.1)
			{
				value3 = $" (Width: {value:+0.0;-0.0;Â±0.0}%, Height: {value2:+0.0;-0.0;Â±0.0}%)";
			}
			PreviewText.Text = $"New size: {result:F1} Ã— {result2:F1} mm{value3}";
		}
		else
		{
			PreviewText.Text = $"Current size: {_currentWidth:F1} Ã— {_currentHeight:F1} mm";
		}
	}

	private void ResizeConfirmButton_Click(object sender, RoutedEventArgs e)
	{
		if (double.TryParse(WidthTextBox.Text, out var result) && double.TryParse(HeightTextBox.Text, out var result2) && double.TryParse(MarginLeftBox.Text, out var result3) && double.TryParse(MarginRightBox.Text, out var result4) && double.TryParse(MarginTopBox.Text, out var result5) && double.TryParse(MarginBottomBox.Text, out var result6))
		{
			if (result <= 0.0 || result2 <= 0.0)
			{
				MessageBox.Show("Width and height must be greater than zero.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			if (result3 < 0.0 || result4 < 0.0 || result5 < 0.0 || result6 < 0.0)
			{
				MessageBox.Show("Margins cannot be negative.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			NewWidth = result;
			NewHeight = result2;
			NewMarginLeft = result3;
			NewMarginRight = result4;
			NewMarginTop = result5;
			NewMarginBottom = result6;
			if (ResizeElementsRadio.IsChecked == true)
			{
				SelectedResizeMode = LabelResizeMode.ResizeElements;
			}
			else if (RepositionElementsRadio.IsChecked == true)
			{
				SelectedResizeMode = LabelResizeMode.RepositionElements;
			}
			else
			{
				SelectedResizeMode = LabelResizeMode.CanvasOnly;
			}
			base.DialogResult = true;
			Close();
		}
		else
		{
			MessageBox.Show("Please enter valid numeric values for all fields.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}
}
