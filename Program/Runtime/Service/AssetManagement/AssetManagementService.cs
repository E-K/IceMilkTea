﻿// zlib/libpng License
//
// Copyright (c) 2018 Sinoa
//
// This software is provided 'as-is', without any express or implied warranty.
// In no event will the authors be held liable for any damages arising from the use of this software.
// Permission is granted to anyone to use this software for any purpose,
// including commercial applications, and to alter it and redistribute it freely,
// subject to the following restrictions:
//
// 1. The origin of this software must not be misrepresented; you must not claim that you wrote the original software.
//    If you use this software in a product, an acknowledgment in the product documentation would be appreciated but is not required.
// 2. Altered source versions must be plainly marked as such, and must not be misrepresented as being the original software.
// 3. This notice may not be removed or altered from any source distribution.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IceMilkTea.Core;

namespace IceMilkTea.Service
{
    /// <summary>
    /// ゲームアセットの読み込み、取得、管理を総合的に管理をするサービスクラスです
    /// </summary>
    public class AssetManagementService : GameService
    {
        // 定数定義
        private const string ResourcesHostName = "resources";

        // 読み取り専用クラス変数宣言
        private static readonly IProgress<double> EmptyProgress = new Progress<double>(_ => { });

        // メンバ変数定義
        private UriInfoCache uriCache;
        private UnityAssetCache assetCache;
        private AssetBundleCache assetBundleCache;
        private List<AssetBundleManifestFetcher> manifestFetcherList;
        private List<AssetBundleStorage> storageList;
        private List<AssetBundleInstaller> installerList;



        /// <summary>
        /// AssetManagementService のインスタンスを初期化します
        /// </summary>
        public AssetManagementService()
        {
            // サブシステムなどの初期化をする
            uriCache = new UriInfoCache();
            assetCache = new UnityAssetCache();
            assetBundleCache = new AssetBundleCache();
            manifestFetcherList = new List<AssetBundleManifestFetcher>();
            storageList = new List<AssetBundleStorage>();
            installerList = new List<AssetBundleInstaller>();
        }


        /// <summary>
        /// マニフェストフェッチャーの追加を行います。
        /// </summary>
        /// <param name="fetcher">追加するフェッチャー</param>
        /// <exception cref="ArgumentNullException">fetcher が null です</exception>
        /// <exception cref="ArgumentException">既に追加済みの fetcher です</exception>
        public void AddManifestFetcher(AssetBundleManifestFetcher fetcher)
        {
            // null を渡されたら
            if (fetcher == null)
            {
                // nullは許されない
                throw new ArgumentNullException(nameof(fetcher));
            }


            // 既に追加済みのフェッチャーだったら
            if (manifestFetcherList.Contains(fetcher))
            {
                // 多重追加は許されない
                throw new ArgumentException($"既に追加済みの {nameof(fetcher)} です");
            }


            // 追加する
            manifestFetcherList.Add(fetcher);
        }


        /// <summary>
        /// ストレージの追加を行います。
        /// </summary>
        /// <param name="storage">追加するストレージ</param>
        /// <exception cref="ArgumentNullException">storage が null です</exception>
        /// <exception cref="ArgumentException">既に追加済みの storage です</exception>
        public void AddStorage(AssetBundleStorage storage)
        {
            // null を渡されたら
            if (storage == null)
            {
                // null は許されない
                throw new ArgumentNullException(nameof(storage));
            }


            // 既に追加済みのストレージだったら
            if (storageList.Contains(storage))
            {
                // 多重追加は許されない
                throw new ArgumentException($"既に追加済みの {nameof(storage)} です");
            }


            // 追加する
            storageList.Add(storage);
        }


        /// <summary>
        /// インストーラの追加を行います。
        /// </summary>
        /// <param name="installer"></param>
        public void AddInstaller(AssetBundleInstaller installer)
        {
            // null を渡されたら
            if (installer == null)
            {
                // null は許されない
                throw new ArgumentNullException(nameof(installer));
            }


            // 既に追加済みのインストーラだったら
            if (installerList.Contains(installer))
            {
                // 多重追加は許されない
                throw new ArgumentException($"既に追加済みの {nameof(installer)} です");
            }


            // 追加する
            installerList.Add(installer);
        }


        /// <summary>
        /// 指定されたアセットURLのアセットを非同期でロードします
        /// </summary>
        /// <typeparam name="T">ロードするアセットの型</typeparam>
        /// <param name="assetUrl">ロードするアセットのURL</param>
        /// <returns>指定されたアセットの非同期ロードを操作しているタスクを返します</returns>
        /// <exception cref="ArgumentNullException">assetUrl が null です</exception>
        /// <exception cref="InvalidOperationException">指定されたアセットのロードに失敗しました Url={assetUrl}</exception>
        public ImtTask<T> LoadAssetAsync<T>(string assetUrl) where T : UnityEngine.Object
        {
            // 進捗通知を受けずに非同期ロードを行う
            return LoadAssetAsync<T>(assetUrl, null);
        }


        /// <summary>
        /// 指定されたアセットURLのアセットを非同期でロードします
        /// </summary>
        /// <typeparam name="T">ロードするアセットの型</typeparam>
        /// <param name="assetUrl">ロードするアセットのURL</param>
        /// <param name="progress">アセットロードの進捗通知を受ける IProgress</param>
        /// <returns>指定されたアセットの非同期ロードを操作しているタスクを返します</returns>
        /// <exception cref="ArgumentNullException">assetUrl が null です</exception>
        /// <exception cref="InvalidOperationException">指定されたアセットのロードに失敗しました Url={assetUrl}</exception>
        public ImtTask<T> LoadAssetAsync<T>(string assetUrl, IProgress<double> progress) where T : UnityEngine.Object
        {
            // ロードの非同期タスクを生成して返す
            return new ImtTask<T>(async () =>
            {
                // もしURLがnullなら
                if (assetUrl == null)
                {
                    // 何をロードするのか不明
                    throw new ArgumentNullException(nameof(assetUrl));
                }


                // UriキャッシュからUri情報を取得する
                var uriInfo = uriCache.GetOrCreateUri(assetUrl);


                // もしアセットキャッシュからアセットを取り出せるのなら
                UnityEngine.Object asset;
                if (assetCache.TryGetAsset(uriInfo, out asset))
                {
                    // このアセットを返す
                    return (T)asset;
                }


                // プログレスが null なら空のプログレスを設定する
                progress = progress ?? EmptyProgress;


                // ホスト名（ストレージ名）を取得してもし Resources なら.
                var storageName = uriInfo.Uri.Host;
                if (storageName == ResourcesHostName)
                {
                    // Resoucesからアセットをロードする
                    asset = await LoadResourcesAssetAsync<T>(uriInfo, progress);
                }
                else
                {
                    // Resourcesでないならアセットバンドル側からロードする
                    asset = await LoadAssetBundleAssetAsync<T>(storageName, uriInfo, progress);
                }


                // もしアセットのロードに失敗していたら
                if (asset == null)
                {
                    // アセットのロードに失敗したことを通知する
                    throw new InvalidOperationException($"指定されたアセットのロードに失敗しました Url={assetUrl}");
                }


                // 読み込まれたアセットをキャッシュに追加して返す
                assetCache.CacheAsset(uriInfo, asset);
                return (T)asset;
            });
        }


        private async Task<T> LoadResourcesAssetAsync<T>(UriInfo assetUrl, IProgress<double> progress) where T : UnityEngine.Object
        {
            throw new NotImplementedException();
        }


        private async Task<T> LoadAssetBundleAssetAsync<T>(string storageName, UriInfo assetUrl, IProgress<double> progress) where T : UnityEngine.Object
        {
            throw new NotImplementedException();
        }
    }
}