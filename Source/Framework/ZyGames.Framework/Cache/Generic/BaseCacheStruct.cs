﻿/****************************************************************************
Copyright (c) 2013-2015 scutgame.com

http://www.scutgame.com

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using ZyGames.Framework.Collection.Generic;
using ZyGames.Framework.Common;
using ZyGames.Framework.Common.Log;
using ZyGames.Framework.Model;
using ZyGames.Framework.Net;
using ZyGames.Framework.Redis;

namespace ZyGames.Framework.Cache.Generic
{
    /// <summary>
    /// 缓存结构基类
    /// </summary>
    /// <typeparam name="T">AbstractEntity类型</typeparam>
    public abstract class BaseCacheStruct<T> : BaseDisposable where T : AbstractEntity, new()
    {
        private const string PrimaryKeyFormat = "EntityPrimaryKey_{0}";
        static BaseCacheStruct()
        {
            try
            {
                var schema = EntitySchemaSet.InitSchema(typeof(T));
                if (schema.IncreaseStartNo > 0)
                {
                    string key = string.Format(PrimaryKeyFormat, schema.EntityName);
                    RedisConnectionPool.SetNo(key, schema.IncreaseStartNo);
                }
                CacheFactory.RegistUpdateNotify(new DefaultCacheStruct<T>());
            }
            catch (Exception)
            { }
        }
        /// <summary>
        /// 
        /// </summary>
        protected IDataContainer<T> DataContainer;

        /// <summary>
        /// 
        /// </summary>
        protected BaseCacheStruct()
        {
            IsReadonly = false;
            SchemaTable schema;
            if (EntitySchemaSet.TryGet<T>(out schema))
            {
                IsReadonly = schema.AccessLevel == AccessLevel.ReadOnly;
            }
            DataContainer = CacheFactory.GetOrCreate<T>(IsReadonly, LoadFactory, LoadItemFactory);
        }

        /// <summary>
        /// 创建Redis主键
        /// </summary>
        /// <param name="personalKey"></param>
        /// <returns></returns>
        public string CreateRedisKey(string personalKey = "")
        {
            return EntitySchemaSet.GenerateRedisKey<T>(personalKey);
        }

        /// <summary>
        /// 获取实体的下个编号
        /// </summary>
        /// <returns></returns>
        public long GetNextNo()
        {
            string key = string.Format(PrimaryKeyFormat, typeof(T).Name);
            return RedisConnectionPool.GetNextNo(key);
        }

        /// <summary>
        /// 是否只读
        /// </summary>
        public bool IsReadonly
        {
            get;
            private set;
        }

        /// <summary>
        /// 是否空
        /// </summary>
        public bool IsEmpty
        {
            get { return DataContainer.IsEmpty; }
        }

        /// <summary>
        /// Get ItemSet array
        /// </summary>
        public CacheItemSet[] ChildrenItem
        {
            get { return DataContainer.ChildrenItem; }
        }

        /// <summary>
        /// 数据是否改变
        /// </summary>
        /// <param name="key">实体Key或personerId</param>
        /// <returns></returns>
        protected bool HasChangeCache(string key)
        {
            return DataContainer.HasChange(key);
        }

        /// <summary>
        /// 尝试从DB中恢复数据
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="personalId"></param>
        /// <returns></returns>
        public bool TryRecoverFromDb(DbDataFilter filter, string personalId = "")
        {
            List<T> dataList;
            string redisKey = CreateRedisKey(personalId);
            TransReceiveParam receiveParam = new TransReceiveParam(redisKey);
            receiveParam.Schema = SchemaTable();
            receiveParam.DbFilter = filter;
            receiveParam.Capacity = receiveParam.Schema.Capacity;
            if (DataContainer.TryRecoverFromDb(receiveParam, out dataList))
            {
                return InitCache(dataList, receiveParam.Schema.PeriodTime);
            }
            return false;
        }
        /// <summary>
        /// 获取实体数据架构信息
        /// </summary>
        /// <returns>返回有Null值</returns>
        protected SchemaTable SchemaTable()
        {
            SchemaTable schemaTable;
            if (EntitySchemaSet.TryGet<T>(out schemaTable))
            {
                return schemaTable;
            }
            return null;
        }

        /// <summary>
        /// 遍历实体方式
        /// </summary>
        /// <param name="func"></param>
        protected void ForeachEntity(Func<string, T, bool> func)
        {
            DataContainer.ForeachEntity(func);
        }

        /// <summary>
        /// 遍历分组方式
        /// </summary>
        /// <param name="func">第一个参数为分组Key,第二个为实体Key</param>
        protected void ForeachGroup(Func<string, string, T, bool> func)
        {
            DataContainer.ForeachGroup(func);
        }
        /// <summary>
        /// 尝试获取实体
        /// </summary>
        /// <param name="groupKey"></param>
        /// <param name="entiyData"></param>
        /// <returns></returns>
        protected bool TryGetEntity(string groupKey, out T entiyData)
        {
            return DataContainer.TryGetEntity(groupKey, out entiyData);
        }

        /// <summary>
        /// 尝试获取分组
        /// </summary>
        /// <param name="groupKey"></param>
        /// <param name="collection"></param>
        /// <param name="loadStatus"></param>
        /// <returns></returns>
        protected bool TryGetGroup(string groupKey, out BaseCollection collection, out LoadingStatus loadStatus)
        {
            return DataContainer.TryGetGroup(groupKey, out collection, out loadStatus);
        }

        /// <summary>
        /// 尝试获取队列
        /// </summary>
        /// <param name="groupKey"></param>
        /// <param name="queue"></param>
        /// <returns></returns>
        protected bool TryGetQueue(string groupKey, out CacheQueue<T> queue)
        {
            return DataContainer.TryGetQueue(groupKey, out queue);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupKey"></param>
        /// <param name="key"></param>
        /// <param name="t"></param>
        /// <param name="periodTime"></param>
        /// <returns></returns>
        protected bool TryAddGroup(string groupKey, string key, T t, int periodTime)
        {
            return DataContainer.TryAddGroup(groupKey, key, t, periodTime);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupKey"></param>
        /// <param name="key"></param>
        /// <param name="t"></param>
        /// <param name="periodTime"></param>
        /// <returns></returns>
        protected bool AddOrUpdateGroup(string groupKey, string key, T t, int periodTime)
        {
            return DataContainer.AddOrUpdateGroup(groupKey, key, t, periodTime);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="t"></param>
        /// <param name="periodTime"></param>
        /// <returns></returns>
        protected bool TryAddEntity(string key, T t, int periodTime)
        {
            return DataContainer.TryAddEntity(key, t, periodTime);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="t"></param>
        /// <param name="periodTime"></param>
        /// <returns></returns>
        protected bool AddOrUpdateEntity(string key, T t, int periodTime)
        {
            return DataContainer.AddOrUpdateEntity(key, t, periodTime);
        }
        /// <summary>
        /// 加载数据工厂
        /// </summary>
        /// <returns></returns>
        protected abstract bool LoadFactory();

        /// <summary>
        /// 加载子项数据工厂
        /// </summary>
        /// <returns></returns>
        protected abstract bool LoadItemFactory(string key);

        /// <summary>
        /// 是否存在实体
        /// </summary>
        /// <returns></returns>
        protected bool IsExistEntity(Predicate<T> match)
        {
            return DataContainer.IsExistEntity(match);
        }
        /// <summary>
        /// 是否存在分组
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        protected bool IsExistGroup(Predicate<T> match)
        {
            return DataContainer.IsExistGroup(match);
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        /// <param name="groupKey"></param>
        /// <param name="dataFilter"></param>
        /// <param name="periodTime"></param>
        /// <returns></returns>
        protected bool TryLoadCache(string groupKey, DbDataFilter dataFilter, int periodTime)
        {
            string redisKey = CreateRedisKey(groupKey);
            SchemaTable schema;
            if (EntitySchemaSet.TryGet<T>(out schema))
            {
                TransReceiveParam receiveParam = new TransReceiveParam(redisKey, schema, dataFilter.Capacity, dataFilter);
                return TryLoadCache(groupKey, receiveParam, periodTime);
            }
            return false;
        }

        /// <summary>
        /// 加载Enity数据
        /// </summary>
        /// <param name="receiveParam"></param>
        /// <param name="periodTime"></param>
        /// <returns></returns>
        protected bool TryLoadCache(TransReceiveParam receiveParam, int periodTime)
        {
            try
            {
                List<T> dataList;
                if (DataContainer.TryReceiveData(receiveParam, out dataList))
                {
                    if (dataList.Count == 0) return true;
                    return InitCache(dataList, periodTime);
                }
                TraceLog.WriteError("Try load cache data:{0} error.", receiveParam.Schema.EntityType.FullName);
            }
            catch (Exception ex)
            {
                string name = receiveParam.Schema != null ? receiveParam.Schema.EntityName : "";
                TraceLog.WriteError("Try load cache \"{0}\" data error:{1}", name, ex);
            }
            return false;
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        /// <param name="groupKey"></param>
        /// <param name="receiveParam"></param>
        /// <param name="periodTime">缓存的生命周期，单位秒</param>
        /// <returns></returns>
        protected bool TryLoadCache(string groupKey, TransReceiveParam receiveParam, int periodTime)
        {
            CacheItemSet itemSet = InitContainer(groupKey, periodTime);
            List<T> dataList;
            if (DataContainer.TryReceiveData(receiveParam, out dataList))
            {
                InitCache(dataList, periodTime);
                itemSet.OnLoadSuccess();
                return true;
            }
            itemSet.OnLoadError();
            TraceLog.WriteError("Try load cache data:{0} error.", typeof(T).FullName);
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="receiveParam"></param>
        /// <param name="match"></param>
        protected void LoadFrom(TransReceiveParam receiveParam, Predicate<T> match)
        {
            List<T> dataList = DataContainer.LoadFrom<T>(receiveParam);
            if (DataContainer.LoadStatus == LoadingStatus.Success && dataList != null)
            {
                int periodTime = receiveParam.Schema.PeriodTime;
                var tempList = match == null ? dataList : dataList.FindAll(match);
                var pairs = tempList.GroupBy(t => t.PersonalId).ToList();
                foreach (var pair in pairs)
                {
                    CacheItemSet itemSet = InitContainer(pair.Key, periodTime);
                    InitCache(pair.ToList(), periodTime);
                    itemSet.OnLoadSuccess();
                }
            }
        }

        /// <summary>
        /// 加载指定Key数据
        /// </summary>
        /// <param name="groupKey"></param>
        /// <returns></returns>
        protected bool TryLoadItem(string groupKey)
        {
            return DataContainer.LoadItem(groupKey);
        }

        /// <summary>
        /// 重新加载，先更新变动的数据，再加载
        /// </summary>
        public void ReLoad(string groupKey = "")
        {
            Update();
            DataContainer.ReLoad();
            if (!string.IsNullOrEmpty(groupKey))
            {
                DataContainer.ReLoadItem(groupKey);
            }
            TraceLog.ReleaseWrite("ReLoad {0} end", DataContainer.RootKey);
        }

        /// <summary>
        /// 卸载数据
        /// </summary>
        public bool UnLoad()
        {
            return DataContainer.TryRemove(c => true);
        }

        /// <summary>
        /// 初始化容器
        /// </summary>
        /// <param name="groupKey"></param>
        /// <param name="periodTime"></param>
        protected virtual CacheItemSet InitContainer(string groupKey, int periodTime)
        {
            //表中记录为空时，增加空项
            return DataContainer.InitGroupContainer(groupKey, periodTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataList"></param>
        /// <param name="periodTime"></param>
        /// <returns></returns>
        protected virtual bool InitCache(List<T> dataList, int periodTime)
        {
            foreach (var data in dataList)
            {
                if (data == null) continue;
                string personalId = data.PersonalId;
                if (string.IsNullOrEmpty(personalId))
                {
                    throw new NotImplementedException(string.Format("The entity {0} \"PersonalId\" not implemented", DataContainer.RootKey));
                }
                data.Reset();
                string key = data.GetKeyCode();
                bool result = DataContainer.AddOrUpdateGroup(personalId, key, data, periodTime);
                if (!result)
                {
                    TraceLog.WriteError("Load data:\"{0}\" tryadd key:\"{1}\" error.", DataContainer.RootKey, key);
                    return false;
                }
                DataContainer.UnChangeNotify(personalId);
            }
            return true;
        }

        /// <summary>
        /// 尝试移除
        /// </summary>
        /// <param name="groupKey"></param>
        /// <param name="subKey"></param>
        /// <param name="callback">移除成功回调</param>
        /// <returns></returns>
        protected bool TryRemove(string groupKey, string subKey, Func<T, bool> callback)
        {
            return DataContainer.TryRemove(groupKey, subKey, callback);
        }

        /// <summary>
        /// 尝试移除
        /// </summary>
        /// <param name="groupKey"></param>
        /// <param name="callback">移除成功回调</param>
        /// <returns></returns>
        protected bool TryRemove(string groupKey, Func<CacheItemSet, bool> callback)
        {
            return DataContainer.TryRemove(groupKey, callback);
        }

        /// <summary>
        /// 更新改变的数据到库中
        /// </summary>
        public void Update()
        {
            Update(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isChange">更新到库中是全部数据还是改变的数据</param>
        public abstract void Update(bool isChange);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isChange">更新到库中是全部数据还是改变的数据</param>
        /// <param name="changeKey"></param>
        protected void UpdateEntity(bool isChange = true, string changeKey = null)
        {
            var changeList = DataContainer.GetChangeEntity(changeKey, isChange);
            TransSendParam sendParam = new TransSendParam() { IsChange = isChange };
            DoSend(changeList, sendParam);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isChange">更新到库中是全部数据还是改变的数据</param>
        /// <param name="changeKey"></param>
        protected void UpdateGroup(bool isChange = true, string changeKey = null)
        {
            var changeList = DataContainer.GetChangeGroup(changeKey, isChange);
            TransSendParam sendParam = new TransSendParam() { IsChange = isChange };
            DoSend(changeList, sendParam);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isChange">更新到库中是全部数据还是改变的数据</param>
        /// <param name="changeKey"></param>
        protected void UpdateQueue(bool isChange = true, string changeKey = null)
        {
            var changeList = DataContainer.GetChangeQueue(changeKey, isChange);
            TransSendParam sendParam = new TransSendParam() { IsChange = isChange };
            DoSend(changeList, sendParam);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataList"></param>
        /// <param name="sendParam"></param>
        protected void DoSend(IEnumerable<KeyValuePair<string, T>> dataList, TransSendParam sendParam)
        {
            var keyValuePairs = dataList as KeyValuePair<string, T>[] ?? dataList.ToArray();
            DataContainer.SendData(keyValuePairs.Select(pair => pair.Value).ToArray(), sendParam);
            var groupList = keyValuePairs.GroupBy(t => t.Key);
            foreach (var g in groupList)
            {
                DataContainer.UnChangeNotify(g.Key);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupList"></param>
        /// <param name="sendParam"></param>
        protected void DoTimeSend(GroupList<string, T> groupList, TransSendParam sendParam)
        {

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //DataContainer = null;
            }
            base.Dispose(disposing);
        }
    }
}