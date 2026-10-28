namespace Batch_Loop_Async.UniTask
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    public static class UniTaskBatchLoopAsync
    {
        #region Common Loop

        /// <summary>
        /// Menjalankan loop batch per frame (forward/reverse) untuk menghindari freeze/frame drop di Unity.
        /// </summary>
        public static async UniTask UniTaskBatchLoop(int totalIterations, int batchSize, Action<int> process,
            bool reverse = false, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default)
        {
            if (totalIterations <= 0)
            {
                Debug.LogWarning("Total iterations must be greater than 0");
                return;
            }

            batchSize = Mathf.Max(1, batchSize);

            if (!reverse)
            {
                for (int i = 0; i < totalIterations; i += batchSize)
                {
                    int end = Mathf.Min(i + batchSize, totalIterations);
                    for (int j = i; j < end; j++)
                        process(j);
                    await UniTask.NextFrame(timing, cancellationToken);
                }
            }
            else
            {
                for (int i = totalIterations; i > 0; i -= batchSize)
                {
                    int start = Mathf.Max(i - batchSize, 0);
                    for (int j = i - 1; j >= start; j--)
                        process(j);
                    await UniTask.NextFrame(timing, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Overload untuk memproses koleksi IReadOnlyList (List, Array, dll.).
        /// Memanggil fungsi utama UniTaskBatchLoop agar konsisten dengan pola GetAll.
        /// </summary>
        public static async UniTask UniTaskBatchLoop<T>(IReadOnlyList<T> listData, int batchSize, Action<T> process,
            bool reverse = false, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default)
        {
            if (listData == null)
            {
                Debug.LogWarning("List data must not be null");
                return;
            }

            await UniTaskBatchLoop(listData.Count, batchSize, (i) => process(listData[i]),
                reverse, timing, cancellationToken);
        }

        #endregion

        #region Get All Qualified

        /// <summary>
        /// Menjalankan loop batch dan mengembalikan kumpulan hasilnya ke dalam Array (Forward/Reverse).
        /// Menggunakan UniTaskBatchLoop (CommonLoop) agar konsisten dan terpusat.
        /// </summary>
        public static async UniTask<TResult[]> UniTaskBatchLoopGetAll<TResult>(
            int totalIterations,
            int batchSize,
            Func<int, TResult> process,
            bool reverse = false,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default)
        {

            if (totalIterations <= 0)
            {
                Debug.LogWarning("Total iterations must be greater than 0");
                return Array.Empty<TResult>();
            }

            var results = new List<TResult>(totalIterations);

            await UniTaskBatchLoop(totalIterations, batchSize,
                (i) =>
                {
                    TResult result = process(i);
                    if (result != null)
                        results.Add(result);
                },
                reverse, timing, cancellationToken);

            return results.ToArray();
        }

        /// <summary>
        /// Overload untuk memproses List/Array sumber dan mengembalikan Array hasil (mendukung konversi tipe data).
        /// </summary>
        public static async UniTask<TResult[]> UniTaskBatchLoopGetAll<TSource, TResult>(
            IReadOnlyList<TSource> listData,
            int batchSize,
            Func<TSource, TResult> process,
            bool reverse = false,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default)
        {
            if (listData == null)
            {
                Debug.LogWarning("List data must not be null");
                return Array.Empty<TResult>();
            }

            return await UniTaskBatchLoopGetAll(
                listData.Count,
                batchSize,
                (i) => process(listData[i]),
                reverse,
                timing,
                cancellationToken);
        }

        #endregion

        #region Get One First

        /// <summary>
        /// Mencari dan mengembalikan 1 item pertama yang tidak null (Forward/Reverse).
        /// Loop akan berhenti seketika (early return) begitu item ditemukan.
        /// </summary>
        public static async UniTask<TResult> UniTaskBatchLoopGetOne<TResult>(
            int totalIterations,
            int batchSize,
            Func<int, TResult> process,
            bool reverse = false,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default)
            where TResult : class
        {
            if (totalIterations <= 0)
            {
                Debug.LogWarning("Total iterations must be greater than 0");
                return null;
            }

            batchSize = Mathf.Max(1, batchSize);

            if (!reverse)
            {
                for (int i = 0; i < totalIterations; i += batchSize)
                {
                    int end = Mathf.Min(i + batchSize, totalIterations);
                    for (int j = i; j < end; j++)
                    {
                        TResult result = process(j);
                        if (result != null)
                            return result; // Langsung berhenti seketika saat ketemu
                    }

                    await UniTask.NextFrame(timing, cancellationToken);
                }
            }
            else
            {
                for (int i = totalIterations; i > 0; i -= batchSize)
                {
                    int start = Mathf.Max(i - batchSize, 0);
                    for (int j = i - 1; j >= start; j--)
                    {
                        TResult result = process(j);
                        if (result != null)
                            return result; // Langsung berhenti seketika saat ketemu
                    }

                    await UniTask.NextFrame(timing, cancellationToken);
                }
            }

            return null;
        }

        /// <summary>
        /// Overload GetOne untuk koleksi IReadOnlyList (List, Array, dll.) dengan dukungan konversi tipe data.
        /// </summary>
        public static async UniTask<TResult> UniTaskBatchLoopGetOne<TSource, TResult>(
            IReadOnlyList<TSource> listData,
            int batchSize,
            Func<TSource, TResult> process,
            bool reverse = false,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default)
            where TResult : class
        {
            if (listData == null)
            {
                Debug.LogWarning("List data must not be null");
                return null;
            }

            return await UniTaskBatchLoopGetOne(
                listData.Count,
                batchSize,
                (i) => process(listData[i]),
                reverse,
                timing,
                cancellationToken);
        }


        #endregion

        #region Get One Compare

        /// <summary>
        /// Menyisir SELURUH data untuk mencari 1 item terbaik (misal: mencari nilai terbesar, terkecil, atau musuh terdekat).
        /// Elemen pertama otomatis dijadikan patokan awal, lalu sisa elemen dibandingkan satu per satu secara batch per frame.
        /// </summary>
        public static async UniTask<T> UniTaskBatchLoopGetOneEntire<T>(
            IReadOnlyList<T> listData,
            int batchSize,
            Func<T, T, T> compare,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default)
            where T : class
        {
            T best = null;

            await UniTaskBatchLoop(
                listData,
                batchSize,
                (candidate) => { best = compare(best, candidate); },
                reverse: false,
                timing,
                cancellationToken);

            return best;
        }

        /// <summary>
        /// Menyisir SELURUH iterasi untuk mencari 1 item terbaik berdasarkan indeks.
        /// Membandingkan hasil satu per satu secara bertahap (batch) per frame.
        /// </summary>
        public static async UniTask<TResult> UniTaskBatchLoopGetOneEntire<TResult>(
            int totalIterations,
            int batchSize,
            Func<TResult, int, TResult> compare,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default)
            where TResult : class
        {
            TResult best = null;

            await UniTaskBatchLoop(
                totalIterations,
                batchSize,
                (i) => { best = compare(best, i); },
                reverse: false,
                timing,
                cancellationToken);

            return best;
        }

        #endregion
    }
}