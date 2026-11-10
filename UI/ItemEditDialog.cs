using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using TaggJack.Models;

namespace TaggJack.UI;

public partial class ItemEditDialog : Window {
	private readonly bool _isEdit;







	public ItemRecord Item { get; private set; }

	public ItemEditDialog(ItemRecord? existing = null)
	{
		InitializeComponent();
		_isEdit = existing != null;
		Item = ((existing != null) ? new ItemRecord
		{
			ItemCode = existing.ItemCode,
			Gtin = existing.Gtin,
			Description = existing.Description,
			Level1 = existing.Level1,
			Level2 = existing.Level2,
			Level3 = existing.Level3
		} : new ItemRecord());
		ItemCodeBox.Text = Item.ItemCode;
		GtinBox.Text = Item.Gtin;
		DescriptionBox.Text = Item.Description;
		Level1Box.Text = Item.Level1.ToString();
		Level2Box.Text = Item.Level2.ToString();
		Level3Box.Text = Item.Level3.ToString();
		ItemCodeBox.IsReadOnly = _isEdit;
		base.Title = (_isEdit ? "Edit Item" : "Add Item");
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(ItemCodeBox.Text))
		{
			MessageBox.Show("Item code is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (!int.TryParse(Level1Box.Text, out var result) || result <= 0 || !int.TryParse(Level2Box.Text, out var result2) || result2 <= 0 || !int.TryParse(Level3Box.Text, out var result3) || result3 <= 0)
		{
			MessageBox.Show("Level 1, 2, and 3 must be positive integers.", "Validation", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		Item.ItemCode = ItemCodeBox.Text.Trim();
		Item.Gtin = GtinBox.Text.Trim();
		Item.Description = DescriptionBox.Text.Trim();
		Item.Level1 = result;
		Item.Level2 = result2;
		Item.Level3 = result3;
		base.DialogResult = true;
		Close();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}
}
