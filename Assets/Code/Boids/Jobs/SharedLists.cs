using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;


public struct SharedLists<T>
	where T : unmanaged
{
	#region Attributes

	private int capacityPerList;
    private NativeArray<T> items;
	private NativeArray<int> listSizes;

	#endregion

	#region Properties

	public T this[int li, int index]
	{
		get => items[capacityPerList * li + index];
		set => items[capacityPerList * li + index] = value;
    }

	public int CapacityPerList => capacityPerList;
	public int NumLists => listSizes.Length;

	#endregion
	
	#region Initialization

	public void Init(int capacityPerList, int totalLists)
	{
		this.capacityPerList = capacityPerList;
        int size = totalLists * capacityPerList;

        items = new NativeArray<T>(size, Allocator.Persistent);
		listSizes = new NativeArray<int>(totalLists, Allocator.Persistent);
	}

	#endregion
	
	#region Methods

	public NativeSlice<T> GetSlice(int li)
	{
		int startOffset = li * capacityPerList;
		NativeSlice<T> slice = new NativeSlice<T>(items, startOffset, listSizes[li]);

		return slice;
	}

	public int GetLength(int li)
	{
		return listSizes[li];
	}

	public T GetItem(int li, int index)
	{
        int startOffset = li * capacityPerList;
        return items[startOffset + index];
	}

	public void Clear(int li)
	{
		listSizes[li] = 0;
	}

	public void Add(int li, T item)
	{
		int length = listSizes[li];
        if (length < capacityPerList)
		{
			// add the item at the end of the list
            int startOffset = li * capacityPerList;
            int i = startOffset + length;
			items[i] = item;

			// increase the size
			++length;
			listSizes[li] = length;
		}
	}

	public void Insert(int li, int index, T item)
	{
		int length = listSizes[li];
		if (length < capacityPerList)
		{
			// push everything from index to length one item to the right
			int startOffset = li * capacityPerList;
			for (int i = startOffset + length - 1; i >= startOffset + index; --i)
				items[i] = items[i - 1];

			// set the item in the position
			items[startOffset + index] = item;

            // increase the size
            ++length;
            listSizes[li] = length;
        }
    }

	public void RemoveAt(int li, int index)
	{
        int length = listSizes[li];
        if (length > index)
		{
            // remove the item from the list
            int startOffset = li * capacityPerList;
            for (int i = startOffset + index; i < startOffset + length; ++i)
				items[i] = items[i + 1];

			// update the list info
			--length;
			listSizes[li] = length;
		}
	}

	#endregion
}
