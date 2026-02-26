using System;
using System.Collections.Generic;
using UnityEngine;

namespace VsensAgent.Core
{
    /// <summary>
    /// 服务定位器模式，用于替代 FindObjectOfType 提高性能
    /// 提供 O(1) 的服务查找，避免 FindObjectOfType 的 O(n) 场景扫描
    /// </summary>
    public static class ServiceLocator
    {
        private static Dictionary<Type, object> services = new Dictionary<Type, object>();

        /// <summary>
        /// 注册服务到服务定位器
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <param name="service">服务实例</param>
        public static void Register<T>(T service) where T : class
        {
            Type type = typeof(T);
            if (services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Service {type.Name} already registered. Overwriting.");
            }
            services[type] = service;
            Debug.Log($"[ServiceLocator] ✅ Registered service: {type.Name}");
        }

        /// <summary>
        /// 获取已注册的服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <returns>服务实例，如果未注册则返回 null</returns>
        public static T Get<T>() where T : class
        {
            Type type = typeof(T);
            if (services.TryGetValue(type, out var service))
            {
                return service as T;
            }
            
            Debug.LogWarning($"[ServiceLocator] Service {type.Name} not found. Did you forget to register it?");
            return null;
        }

        /// <summary>
        /// 取消注册服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        public static void Unregister<T>() where T : class
        {
            Type type = typeof(T);
            if (services.ContainsKey(type))
            {
                services.Remove(type);
                Debug.Log($"[ServiceLocator] Unregistered service: {type.Name}");
            }
        }

        /// <summary>
        /// 清除所有已注册的服务（通常在场景切换时使用）
        /// </summary>
        public static void Clear()
        {
            services.Clear();
            Debug.Log("[ServiceLocator] All services cleared");
        }

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <returns>是否已注册</returns>
        public static bool IsRegistered<T>() where T : class
        {
            return services.ContainsKey(typeof(T));
        }
    }
}
