using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SuitLog.API;

public class ItemListWrapper
{
    private readonly ISuitLogAPI _api;
    private readonly MonoBehaviour _itemList;

    public ItemListWrapper(ISuitLogAPI api, MonoBehaviour itemList)
    {
        _api = api;
        _itemList = itemList;
    }
    
    public void Open()
    {
        _api.ItemListOpen(_itemList);
    }

    public void Close()
    {
        _api.ItemListClose(_itemList);
    }

    public int UpdateList()
    {
        return _api.ItemListUpdateList(_itemList);
    }

    public void UpdateListUI()
    {
        _api.ItemListUpdateListUI(_itemList);
    }

    public void SetName(string nameValue)
    {
        _api.ItemListSetName(_itemList, nameValue);
    }

    public void SetItems(List<Tuple<string, bool, bool, bool>> items)
    {
        _api.ItemListSetItems(_itemList, items);
    }

    public int GetSelectedIndex()
    {
        return _api.ItemListGetSelectedIndex(_itemList);
    }

    public void SetSelectedIndex(int index)
    {
        _api.ItemListSetSelectedIndex(_itemList, index);
    }

    public Image GetPhoto()
    {
        return _api.ItemListGetPhoto(_itemList);
    }

    public Text GetQuestionMark()
    {
        return _api.ItemListGetQuestionMark(_itemList);
    }

    public void DescriptionFieldClear()
    {
        _api.ItemListDescriptionFieldClear(_itemList);
    }

    public ShipLogFactListItem DescriptionFieldGetNextItem()
    {
        return _api.ItemListDescriptionFieldGetNextItem(_itemList);
    }

    public void DescriptionFieldOpen()
    {
        _api.ItemListDescriptionFieldOpen(_itemList);
    }

    public void DescriptionFieldClose()
    {
        _api.ItemListDescriptionFieldClose(_itemList);
    }

    public List<ShipLogEntryListItem> GetItemsUI()
    {
        return _api.ItemListGetItemsUI(_itemList);
    }

    public int GetIndexUI(int index)
    {
        return _api.ItemListGetIndexUI(_itemList, index);
    }
}
