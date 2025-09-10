using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebhookService.Infrastructure.Services
{
    /// <summary>
    /// قاطع الدائرة للحماية من الأحمال الزائدة
    /// Circuit breaker for overload protection
    /// CircuitBreaker هو نمط تصميم يستخدم لحماية النظام عند وجود فشل متكرر في العمليات الخارجية.
    /// </summary>
    public class CircuitBreaker
    {
        private readonly int _failureThreshold;                                     // الحد الأقصى للفشل قبل فتح القاطع 
        private readonly TimeSpan _timeout;                                         // مدة الانتظار قبل تجربة HalfOpen
        private readonly ILogger<CircuitBreaker> _logger;
        private int _failureCount;                                                  // عدد المحاولات الفاشلة
        private DateTime _lastFailureTime;                                          // آخر وقت حدوث فشل
        private CircuitBreakerState _state = CircuitBreakerState.Closed;            // حالة القاطع

        public CircuitBreaker(int failureThreshold, TimeSpan timeout, ILogger<CircuitBreaker> logger)
        {
            _failureThreshold = failureThreshold;
            _timeout = timeout;
            _logger = logger;
        }

        /// <summary>
        /// تنفيذ العملية مع حماية قاطع الدائرة
        /// Execute operation with circuit breaker protection
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            if (_state == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime > _timeout)
                {
                    _state = CircuitBreakerState.HalfOpen;
                    _logger.LogInformation("قاطع الدائرة في حالة نصف مفتوح");
                }
                else
                {
                    throw new InvalidOperationException("قاطع الدائرة مفتوح - Circuit breaker is open");
                }
            }

            try
            {
                var result = await operation();
                OnSuccess();
                return result;
            }
            catch (Exception)
            {
                OnFailure();
                throw;
            }
        }

        /// <summary>
        /// معالجة النجاح
        /// Handle success
        /// </summary>
        private void OnSuccess()
        {
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
        }

        /// <summary>
        /// معالجة الفشل
        /// زيادة عداد الفشل، وإذا تجاوز failureThreshold → فتح القاطع (Open).
        /// Handle failure
        /// </summary>
        private void OnFailure()
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitBreakerState.Open;
                _logger.LogWarning("تم فتح قاطع الدائرة بعد {FailureCount} فشل", _failureCount);
            }
        }

        public bool IsOpen => _state == CircuitBreakerState.Open;
    }

    /// <summary>
    /// حالات قاطع الدائرة
    /// Circuit breaker states
    /// </summary>
    public enum CircuitBreakerState
    {
        Closed,   // مغلق - العمليات تعمل بشكل طبيعي
        Open,     // مفتوح - العمليات محظورة
        HalfOpen  // نصف مفتوح - اختبار العمليات
    }
}
