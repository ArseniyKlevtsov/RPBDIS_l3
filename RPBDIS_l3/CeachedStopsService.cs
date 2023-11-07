using Microsoft.Extensions.Caching.Memory;
using RPBDIS_l3.Model;
using System.Threading.Tasks;

namespace RPBDIS_l3
{
    public class CeachedStopsService
    {
        private RailwayTrafficContext _db;
        private IMemoryCache _memoryCache;
        private int _rowsNumber;

        public CeachedStopsService(RailwayTrafficContext context, IMemoryCache memoryCache, int rowNumber=20)
        {
            _db = context;
            _memoryCache = memoryCache;
            _rowsNumber = rowNumber;
        }

        /// <summary>
        /// Возвращает список объектов Stop из RailwayTrafficContext
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Stop> GetStops()
        {
            Console.WriteLine("20 Stops получено из бд");
            return _db.Stops.Take(_rowsNumber).ToList();
        }

        /// <summary>
        /// Добавляет в кэш объекты Stop из RailwayTrafficContext начиная с первого на 2*13+240 сек.
        /// </summary>
        /// <param name="cacheKey"> ключ для данных в кэшэ </param>
        public void AddStops(string cacheKey)
        {
            IEnumerable<Stop> stops = _db.Stops.Take(_rowsNumber);

            _memoryCache.Set(cacheKey, stops, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(266)
            });
            Console.WriteLine("20 Stops загружено в кэш");
        }

        /// <summary>
        /// Берёт из кэша объекты Stop из RailwayTrafficContext 
        /// </summary>
        /// <param name="cacheKey">ключ для данных в кэшэ</param>
        /// <returns></returns>
        public IEnumerable<Stop> GetStops(string cacheKey)
        {
            IEnumerable<Stop> stops = null;
            // если в кэшэ не нашлось записей, они берутся из бд и кладутся в кэш на 2*13+240 сек.
            if (!_memoryCache.TryGetValue(cacheKey, out stops))
            {
                stops = _db.Stops.Take(_rowsNumber).ToList();
                if (stops != null)
                {
                    _memoryCache.Set(cacheKey, stops,
                    new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(266)));
                    Console.WriteLine("20 Stops взято из бд и загружено в кэш");
                }
            }
            else
                Console.WriteLine("20 Stops взято из кэша");
            return stops;
        }

    }
}
