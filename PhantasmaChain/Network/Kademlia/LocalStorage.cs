using System;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
using System.IO;

namespace Phantasma.Network.Kademlia
{

    public class KademliaEntry
    {
        public Endpoint endpoint;
        public DateTime publication;
        public TimeSpan validity;
        public KademliaResource resource;
    }

	/// <summary>
	/// Stores key/value pairs assigned to our node.
	/// Automatically handles persistence to disk.
	/// </summary>
	public class LocalStorage
	{
	
		private Dictionary<ID, KademliaEntry> store = new Dictionary<ID, KademliaEntry>(); 
		private Thread saveThread;
		private string indexFilename;
		private string storageRoot;
		private Mutex mutex;
		
		private const string INDEX_EXTENSION = ".index";
		private const string DATA_EXTENSION = ".dat";
		private static IFormatter coder = new BinaryFormatter(); // For disk storage
		private static TimeSpan SAVE_INTERVAL = new TimeSpan(0, 10, 0);
		
		/// <summary>
		/// Make a new LocalStorage. 
		/// Uses the executing assembly's name to determine the filename for on-disk storage.
		/// If another LocalStorage on the machine is already using that file, we use a temp directory.
		/// </summary>
		public LocalStorage()
		{
			string assembly = Assembly.GetEntryAssembly().GetName().Name;
			string libname = Assembly.GetExecutingAssembly().GetName().Name;
			
			// Check the mutex to see if we get the disk storage
			string mutexName = libname + "-" + assembly + "-storage";
			try {
				mutex = Mutex.OpenExisting(mutexName);
				// If that worked, our disk storage has to be in a temp directory
				storageRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			} catch  {
				// We get the real disk storage
				mutex = new Mutex(true, mutexName);				
				storageRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\" + libname + "\\" + assembly + "\\";
			}
			
			Console.WriteLine("Storing data in " + storageRoot);
			
			// Set a filename for an index file.
			indexFilename =  Path.Combine(storageRoot, "index" + INDEX_EXTENSION);
			
			// Get our store from disk, if possible
			if(File.Exists(indexFilename)) {
				try {
					// Load stuff from disk
					FileStream fs = File.OpenRead(indexFilename);                    
					//store = (SortedList<ID, SortedList<ID, Entry>>) coder.Deserialize(fs);
					fs.Close();

                    throw new NotImplementedException();
                } catch (Exception ex) {
					Console.WriteLine("Could not load disk data: " + ex.ToString());
				}
			}
			
			// If we need a new store, make it
			if(store == null) {
                store = new Dictionary<ID, KademliaEntry>();
			}
			
			// Start the index autosave thread
			saveThread = new Thread(new ThreadStart(BackgroundSave));
			saveThread.IsBackground = true;
			saveThread.Start();
		}
		
		/// <summary>
		/// Clean up and close our mutex if needed.
		/// </summary>
		~LocalStorage()
		{
			saveThread.Abort(); // Stop our autosave thread.
			SaveIndex(); // Make sure our index getw written when we shut down properly.
			mutex.Close(); // Release our hold on the mutex.
		}
	
		/// <summary>
		/// Create all folders in a path, if missing.
		/// </summary>
		/// <param name="path"></param>
		private static void CreatePath(string path)
		{
			path = path.TrimEnd('/', '\\');
			if(Directory.Exists(path)) {
				return; // Base case
			} else {
				if(Path.GetDirectoryName(path) != "") {
					CreatePath(Path.GetDirectoryName(path)); // Make up to parent
				}
				Directory.CreateDirectory(path); // Make this one
			}
		}
		
		/// <summary>
		/// Where should we save a particular value?
		/// </summary>
		/// <param name="key"></param>
		/// <param name="hash"></param>
		/// <returns></returns>
		private string PathFor(ID key, ID hash)
		{
			return Path.Combine(Path.Combine(storageRoot, key.ToString()), hash.ToString() + DATA_EXTENSION);
		}
		
		/// <summary>
		/// Save the store in the background.
		/// PRECONSITION: We have the mutex and diskFilename is set.
		/// </summary>
		private void BackgroundSave()
		{
			while(true) {
				SaveIndex();
				Thread.Sleep(SAVE_INTERVAL);
			}
		}
		
		/// <summary>
		/// Save the index now.
		/// </summary>
		private void SaveIndex() 
		{
			try {
				Console.WriteLine("Saving datastore index...");
				CreatePath(Path.GetDirectoryName(indexFilename));
				
				// Save
				lock(store) {
					FileStream fs = File.OpenWrite(indexFilename);
					coder.Serialize(fs, store);
					fs.Close();
				}
				Console.WriteLine("Datastore index saved");
			} catch (Exception ex) { // Report errors so the thread keeps going
				Console.WriteLine("Save error: " + ex.ToString());
			}
		}

        public KademliaResource Find(ID key)
        {
            lock (store)
            {
                if (store.ContainsKey(key))
                {
                    return store[key].resource;
                }
            }

            return null;
        }

        public void RefreshResource(ID key, Endpoint endpoint, DateTime timestamp)
        {
            lock (store)
            {
                if (store.ContainsKey(key))
                {
                    var entry = store[key];
                    entry.endpoint = endpoint;
                    entry.publication = timestamp; // TODO is this correct?
                }
            }
        }

        public void StoreResource(KademliaResource resource, DateTime publicationTime, Endpoint endpoint)
        {
            var entry = new KademliaEntry();
            entry.resource = resource;
            entry.publication = publicationTime;
            entry.endpoint = endpoint;

            lock (store)
            {
                store[resource.Key] = entry;
            }
        }

        public DateTime GetPublicationTime(ID key, Endpoint endpoint)
        {
            throw new NotImplementedException();
        }

        public void ForEachEntry(Action<KademliaEntry> visitor)
        {
            lock (store)
            {
                foreach (var entry in store.Values)
                {
                    visitor(entry);
                }
            }
        }
		
		/// <summary>
		/// Do we have any data for the given key?
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public bool ContainsKey(ID key)
		{
            bool result;
            lock (store)
            {
                result = store.ContainsKey(key); ;
            }

            return result;
		}	
	
		/// <summary>
		/// Get all IDs, so we can go through and republish everything.
		/// It's a copy so you can iterate it all you want.
		/// </summary>
		public IEnumerable<ID> GetKeys()
		{
			List<ID> toReturn = new List<ID>();
			lock(store) {
				foreach(ID key in store.Keys) {
					toReturn.Add(key);
				}
			}
			return toReturn;
		}
			
		/// <summary>
		/// Expire old entries
		/// </summary>
		public void Expire()
		{
			lock(store) {
                /*		for(int i = 0; i < store.Count; i++) {
                            // Go through every value for the key
                            SortedList<ID, Entry> vals = store.Values[i];
                            for(int j = 0; j < vals.Count; j++) {
                                if(DateTime.Now.ToUniversalTime() 
                                   > vals.Values[j].timestamp + vals.Values[j].keepFor) { // Too old!
                                    // Delete file
                                    string filePath = PathFor(store.Keys[i], vals.Keys[j]);
                                    File.Delete(filePath);

                                    // Remove index
                                    vals.RemoveAt(j);
                                    j--;
                                }
                            }

                            // Don't keep empty value lists around, or their directories
                            if(vals.Count == 0) {
                                string keyPath = Path.Combine(storageRoot, store.Keys[i].ToString());
                                Directory.Delete(keyPath);
                                store.RemoveAt(i);
                                i--;
                            }
                        }*/

                throw new NotImplementedException();
			}
			// TODO: Remove files that the index does not mention!
		}
	}
}
