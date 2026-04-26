using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SuitLog.API;

public class SuitLogAPI : ISuitLogAPI
{
    public void AddMode(ShipLogMode mode, Func<bool> isEnabledSupplier, Func<string> nameSupplier)
    {
        SuitLog.Instance.AddMode(mode, isEnabledSupplier, nameSupplier);
    }
    
    public void ItemListMake(Action<MonoBehaviour> callback)
    {
        SuitLogItemList.Make(callback);
    }

    public void ItemListOpen(MonoBehaviour itemList)
    {
        ((SuitLogItemList)itemList).Open();
    }

    public void ItemListClose(MonoBehaviour itemList)
    {
        ((SuitLogItemList)itemList).Close();
    }

    public int ItemListUpdateList(MonoBehaviour itemList)
    {
        return ((SuitLogItemList)itemList).UpdateList();
    }

    public void ItemListUpdateListUI(MonoBehaviour itemList)
    {
        ((SuitLogItemList)itemList).UpdateListUI();
    }

    public void ItemListSetName(MonoBehaviour itemList, string nameValue)
    {
        ((SuitLogItemList)itemList).SetName(nameValue);
    }

    public void ItemListSetItems(MonoBehaviour itemList, List<Tuple<string, bool, bool, bool>> items)
    {
        ((SuitLogItemList)itemList).contentsItems = items;
    }

    public int ItemListGetSelectedIndex(MonoBehaviour itemList)
    { 
        return ((SuitLogItemList)itemList).selectedIndex;
    }

    public void ItemListSetSelectedIndex(MonoBehaviour itemList, int index)
    {
        ((SuitLogItemList)itemList).selectedIndex = index;
    }

    public Image ItemListGetPhoto(MonoBehaviour itemList)
    {
        return ((SuitLogItemList)itemList).photo;
    }

    public Text ItemListGetQuestionMark(MonoBehaviour itemList)
    {
        return ((SuitLogItemList)itemList).questionMark;
    }

    public void ItemListDescriptionFieldClear(MonoBehaviour itemList)
    {
        ((SuitLogItemList)itemList).DescriptionFieldClear();
    }

    public ShipLogFactListItem ItemListDescriptionFieldGetNextItem(MonoBehaviour itemList)
    {
        return ((SuitLogItemList)itemList).DescriptionFieldGetNextItem();
    }

    public void ItemListDescriptionFieldOpen(MonoBehaviour itemList)
    {
        ((SuitLogItemList)itemList).DescriptionFieldOpen();
    }

    public void ItemListDescriptionFieldClose(MonoBehaviour itemList)
    {
        ((SuitLogItemList)itemList).DescriptionFieldClose();
    }

    public List<ShipLogEntryListItem> ItemListGetItemsUI(MonoBehaviour itemList)
    {
        return ((SuitLogItemList)itemList).uiItems;
    }

    public int ItemListGetIndexUI(MonoBehaviour itemList, int index)
    {
        return ((SuitLogItemList)itemList).GetIndexUI(index);
    }

    public void LockSuitLog(object caller)
    {
        SuitLog.Instance.LockSuitLog(caller);
    }

    public void UnlockSuitLog(object caller)
    {
        SuitLog.Instance.UnlockSuitLog(caller);
    }
}
