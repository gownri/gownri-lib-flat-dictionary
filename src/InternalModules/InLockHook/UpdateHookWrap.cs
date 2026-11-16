//using HashIndex.InternalModules.InLockHook.Interface;
//using System;
//using System.Collections.Generic;
//using System.Text;

//namespace HashIndex.InternalModules.InLockHook;

//internal readonly struct UpdateHookWrap<T, U, TKey, TValue>(
//        TKey oldKey,
//        TValue oldValue, 
//        int oldIndex,
//        T removeHook,
//        U addHook) 
//    : IInLockUpdateHook<TKey, TValue>
//    where T : IInLockRemoveHook<TKey, TValue>
//    where U : IInLockAddHook<TKey, TValue>
//    //where TKey : notnull
//{
//    private readonly T removeHook = removeHook;
//    private readonly U addHook = addHook;
//    private readonly TKey oldKey = oldKey;
//    void IInLockUpdateHook<TKey, TValue>.Update(TKey key, TValue newValue, int newIndex)
//    {
//        removeHook.Remove(key, oldValue, oldIndex);
//        addHook.Add(key, newValue, newIndex);
//    }
//}
