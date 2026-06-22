using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    // Kameranın sallantı bittikten sonra döneceği orijinal pozisyonu
    private Vector3 originalPosition;

    void Start()
    {
        // Oyun başında kameranın durduğu ilk yeri hafızaya alıyoruz
        originalPosition = transform.localPosition;
    }

    // Kedinin vuruş kodundan çağıracağımız sallama metodu
    public void TriggerShake(float duration, float magnitude)
    {
        // Sallama işlemini zaman ayarlı rutin (Coroutine) olarak başlatıyoruz
        StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f; // Geçen süreyi tutan sayaç

        // Belirlediğimiz süre dolana kadar döngüyü çalıştır
        while (elapsed < duration)
        {
            // Rastgele X ve Y koordinatları üreterek kamerayı sarsıyoruz
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            // Kamerayı yeni rastgele pozisyona zıplatıyoruz (Z eksenini -10'da sabit tutuyoruz)
            transform.localPosition = new Vector3(x, y, originalPosition.z);

            // Geçen süreye her kare (frame) arasındaki zamanı ekliyoruz
            elapsed += Time.deltaTime;

            // Bir sonraki kareye kadar Unity'yi bekletiyoruz
            yield return null;
        }

        // Sallanma süresi bittiğinde kamerayı milimetrik olarak eski orijinal yerine oturtuyoruz
        transform.localPosition = originalPosition;
    }
}