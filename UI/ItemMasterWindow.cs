using System;
using System.CodeDom.Compiler;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using TaggJack.Models;
using TaggJack.Services;
using Microsoft.Win32;

namespace TaggJack.UI;

public partial class ItemMasterWindow : Window {
	private readonly SqliteStore _store = new SqliteStore();

	private readonly ObservableCollection<ItemRecord> _items = new ObservableCollection<ItemRecord>();



	public ItemMasterWindow()
	{
		InitializeComponent();
		WindowIconHelper.TrySetWindowIcon(this);
		ItemsGrid.ItemsSource = _items;
		LoadItems();
	}

	private void LoadItems(string? search = null)
	{
		_items.Clear();
		foreach (ItemRecord allItem in _store.GetAllItems(search))
		{
			_items.Add(allItem);
		}
	}

	private void Search_Click(object sender, RoutedEventArgs e)
	{
		LoadItems(SearchBox.Text);
	}

	private void Clear_Click(object sender, RoutedEventArgs e)
	{
		SearchBox.Text = "";
		LoadItems();
	}

	private void Add_Click(object sender, RoutedEventArgs e)
	{
		ItemEditDialog itemEditDialog = new ItemEditDialog
		{
			Owner = this
		};
		if (itemEditDialog.ShowDialog() == true)
		{
			_store.UpsertItem(itemEditDialog.Item);
			LoadItems(SearchBox.Text);
		}
	}

	private void Edit_Click(object sender, RoutedEventArgs e)
	{
		if (!(ItemsGrid.SelectedItem is ItemRecord existing))
		{
			MessageBox.Show("Select an item to edit.", "Item Master", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		ItemEditDialog itemEditDialog = new ItemEditDialog(existing)
		{
			Owner = this
		};
		if (itemEditDialog.ShowDialog() == true)
		{
			_store.UpsertItem(itemEditDialog.Item);
			LoadItems(SearchBox.Text);
		}
	}

	private void Delete_Click(object sender, RoutedEventArgs e)
	{
		if (!(ItemsGrid.SelectedItem is ItemRecord itemRecord))
		{
			MessageBox.Show("Select an item to delete.", "Item Master", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		else if (MessageBox.Show("Delete item '" + itemRecord.ItemCode + "'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
		{
			_store.DeleteItem(itemRecord.ItemCode);
			LoadItems(SearchBox.Text);
		}
	}

	private void Export_Click(object sender, RoutedEventArgs e)
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Filter = "CSV files (*.csv)|*.csv",
			FileName = "item-master.csv"
		};
		if (saveFileDialog.ShowDialog() == true)
		{
			CsvExportHelper.ExportItems(_store.GetAllItems(SearchBox.Text), saveFileDialog.FileName);
			MessageBox.Show("Export completed.", "Item Master", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}
}
