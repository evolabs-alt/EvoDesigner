using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using TaggJack.Models;

namespace TaggJack.UI;

public partial class FieldMappingWindow : Window {
	private List<ComboBox> fieldComboBoxes = new List<ComboBox>();


	public List<UIElement> LabelElements { get; set; }

	public DataTable ResultTable { get; set; }

	public List<FieldMapping> Mappings { get; set; } = new List<FieldMapping>();

	public FieldMappingWindow(List<UIElement> labelElements, DataTable resultTable)
	{
		InitializeComponent();
		LabelElements = labelElements;
		ResultTable = resultTable;
		CreateMappingInterface();
	}

	private void CreateMappingInterface()
	{
		MappingPanel.Children.Clear();
		fieldComboBoxes.Clear();
		List<string> list = new List<string> { "[None - Keep Current Text]" };
		foreach (DataColumn column in ResultTable.Columns)
		{
			list.Add(column.ColumnName + " (" + column.DataType.Name + ")");
		}
		for (int i = 0; i < LabelElements.Count; i++)
		{
			UIElement element = LabelElements[i];
			string elementDescription = GetElementDescription(element, i);
			Border border = new Border
			{
				BorderBrush = Brushes.LightGray,
				BorderThickness = new Thickness(1.0),
				Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
				Padding = new Thickness(10.0)
			};
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(200.0)
			});
			TextBlock element2 = new TextBlock
			{
				Text = elementDescription,
				VerticalAlignment = VerticalAlignment.Center,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
			};
			Grid.SetColumn(element2, 0);
			ComboBox comboBox = new ComboBox
			{
				ItemsSource = list,
				SelectedIndex = 0,
				VerticalAlignment = VerticalAlignment.Center,
				Tag = i
			};
			Grid.SetColumn(comboBox, 1);
			fieldComboBoxes.Add(comboBox);
			grid.Children.Add(element2);
			grid.Children.Add(comboBox);
			border.Child = grid;
			MappingPanel.Children.Add(border);
		}
	}

	private string GetElementDescription(UIElement element, int index)
	{
		string value = "Unknown";
		string value2 = "";
		if (element is Border { Child: TextBlock child })
		{
			if (child.Text.StartsWith("[") && child.Text.EndsWith("]"))
			{
				value = child.Text;
			}
			else
			{
				value = "Text";
				value2 = " (Current: \"" + child.Text + "\")";
			}
		}
		else if (element is TextBlock textBlock)
		{
			if (textBlock.Text.StartsWith("[") && textBlock.Text.EndsWith("]"))
			{
				value = textBlock.Text;
			}
			else
			{
				value = "Text";
				value2 = " (Current: \"" + textBlock.Text + "\")";
			}
		}
		else if (element is Canvas canvas && canvas.Children.Count > 0 && canvas.Children[0] is Line)
		{
			value = "Line";
			value2 = " (Drawing element)";
		}
		else if (element is Image { Tag: var tag } image)
		{
			if (!(tag?.ToString() == "QRCode"))
			{
				object tag2 = image.Tag;
				if (tag2 == null || tag2.ToString()?.StartsWith("QRCodeData:") != true)
				{
					value = "Image";
					if (image.Tag != null)
					{
						string fileName = System.IO.Path.GetFileName(image.Tag.ToString() ?? "");
						value2 = (string.IsNullOrEmpty(fileName) ? "" : (" (Source: " + fileName + ")"));
					}
					else
					{
						value2 = "";
					}
					goto IL_0294;
				}
			}
			value = "[QR Code]";
			object tag3 = image.Tag;
			if (tag3 != null && tag3.ToString()?.StartsWith("QRCodeData:") == true)
			{
				value2 = " (Current: \"" + image.Tag.ToString().Substring("QRCodeData:".Length) + "\")";
			}
		}
		else if (element is Rectangle { Tag: var tag4 })
		{
			if (tag4?.ToString() == "No Image Selected")
			{
				value = "Image Placeholder";
				value2 = " (No image selected)";
			}
			else
			{
				value = "Rectangle";
				value2 = " (Shape element)";
			}
		}
		goto IL_0294;
		IL_0294:
		return $"Element {index + 1}: {value}{value2}";
	}

	private void OK_Click(object sender, RoutedEventArgs e)
	{
		Mappings.Clear();
		for (int i = 0; i < fieldComboBoxes.Count; i++)
		{
			int selectedIndex = fieldComboBoxes[i].SelectedIndex;
			if (selectedIndex > 0)
			{
				string columnName = ResultTable.Columns[selectedIndex - 1].ColumnName;
				Mappings.Add(new FieldMapping
				{
					ElementIndex = i,
					FieldName = columnName
				});
			}
		}
		base.DialogResult = true;
		Close();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}
}
