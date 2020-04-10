using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Threading;
using System.Collections.Concurrent;

namespace VSPackage.CPPCheckPlugin
{
    public class ThreadManager
    {
        public delegate void Callback(SourceFile file, bool isChanged, ManualResetEvent killEvent);
        public ThreadManager(int threadsNum, Callback callback)
        {
            m_callback = callback;
            m_queueSemaphore = new Semaphore(0, 100);
            for (int i = 0; i < threadsNum; i++)
            {
                m_events.Add(new AutoResetEvent(false));
                m_killEvents.Add(new ManualResetEvent(false));
                m_killedEvents.Add(new AutoResetEvent(false));
                m_currentFiles.Add(null);
                m_changed.Add(false);
                m_threads.Add(new Thread(threadInnerCallback));
            }

            for (int i = 0; i < threadsNum; i++)
            {
                m_threads[i].Start(i);
            }
        }

        public void Add(SourceFile file, bool isChanged)
        {
            var thread = new Thread(() =>
            {
                m_addMutex.WaitOne();

                int threadIndexToKill = -1;

                for (int i = 0; i < m_currentFiles.Count; i++)
                {
                    var file2 = m_currentFiles[i];
                    if (null != file2 && file2.FilePath == file.FilePath)
                    {
                        if (!isChanged)
                        {
                            m_addMutex.ReleaseMutex();
                            return;
                        }
                        threadIndexToKill = i;
                        break;
                    }
                }

                if (threadIndexToKill == -1)
                {
                    for (int i = 0; i < m_currentFiles.Count; i++)
                    {
                        if (m_currentFiles[i] == null)
                        {
                            threadIndexToKill = i;
                            break;
                        }
                    }
                }

                m_queueMutex.WaitOne();
                for (int i = 0; i < m_queuedFiles.Count; i++)
                {
                    if (m_queuedFiles[i].Item1.FilePath == file.FilePath)
                    {
                        m_queueSemaphore.WaitOne();
                        m_queuedFiles.Remove(m_queuedFiles[i]);
                        break;
                    }
                }
                m_queueMutex.ReleaseMutex();

                if (threadIndexToKill == -1)
                {
                    int rInt = r.Next(0, m_threads.Count); //for ints
                    threadIndexToKill = rInt;
                    m_queueMutex.WaitOne();
                    m_queuedFiles.Add((m_currentFiles[rInt], isChanged));
                    m_queueSemaphore.Release();
                    m_queueMutex.ReleaseMutex();
                }

                m_currentFiles[threadIndexToKill] = file;
                m_changed[threadIndexToKill] = isChanged;
                m_addMutex.ReleaseMutex();
                m_killEvents[threadIndexToKill].Set();

                m_killedEvents[threadIndexToKill].WaitOne();

                m_killEvents[threadIndexToKill].Reset();

                m_currentFiles[threadIndexToKill] = file;

                m_events[threadIndexToKill].Set();

            });
            thread.Start();
        }

        bool safeReplaceInCurrentFiles(int threadIndex, SourceFile wanted, SourceFile value)
        {
            m_currentFilesMutex.WaitOne();
            if (m_currentFiles[threadIndex] == wanted)
            {
                m_currentFiles[threadIndex] = value;
                m_currentFilesMutex.ReleaseMutex();
                return true;
            }
            m_currentFilesMutex.ReleaseMutex();
            return false;
        }

        private void threadInnerCallback(object obj)
        {
            var threadIndex = (int)obj;
            while (true)
            {
                try
                {
                    List<WaitHandle> waitHandles;
                    waitHandles = new List<WaitHandle> { m_events[threadIndex], m_killEvents[threadIndex] };

                    int waitedIndex = WaitHandle.WaitAny(waitHandles.ToArray());
                    if (1 == waitedIndex)
                    {
                        throw new ThreadAbortException();
                    }

                    var file = m_currentFiles[threadIndex];
                    m_callback(file, m_changed[threadIndex], m_killEvents[threadIndex]);
                    safeReplaceInCurrentFiles(threadIndex, file, null);
                    while (true)
                    {
                        WaitHandle[] waitHandles2 = { m_killEvents[threadIndex], m_queueSemaphore };
                        int waitedIndex2 = WaitHandle.WaitAny(waitHandles2);
                        if (0 == waitedIndex2)
                        {
                            throw new ThreadAbortException();
                        }
                        m_queueMutex.WaitOne();
                        var (file2, isChanged2) = m_queuedFiles.Last();
                        if (!safeReplaceInCurrentFiles(threadIndex, file, file2))
                        {
                            m_queueMutex.ReleaseMutex();
                            m_queueSemaphore.Release();
                            break;
                        }
                        m_queuedFiles.Remove((file2, isChanged2));
                        m_queueMutex.ReleaseMutex();

                        m_callback(file2, m_changed[threadIndex], m_killEvents[threadIndex]);
                        safeReplaceInCurrentFiles(threadIndex, file, null);
                        file = file2;
                    }

                }
                catch (ThreadAbortException)
                {
                    m_killedEvents[threadIndex].Set();
                }
            }
        }

        private List<AutoResetEvent> m_events = new List<AutoResetEvent>();
        Mutex m_addMutex = new Mutex();
        Mutex m_currentFilesMutex = new Mutex();
        Mutex m_queueMutex = new Mutex();
        List<bool> m_changed = new List<bool>();
        private Semaphore m_queueSemaphore;
        private List<ManualResetEvent> m_killEvents = new List<ManualResetEvent>();
        private List<AutoResetEvent> m_killedEvents = new List<AutoResetEvent>();
        private List<Thread> m_threads = new List<Thread>();
        private List<SourceFile> m_currentFiles = new List<SourceFile>();
        private List<(SourceFile, bool)> m_queuedFiles = new List<(SourceFile, bool)>();
        Callback m_callback;
        Random r = new Random();
    }
}
