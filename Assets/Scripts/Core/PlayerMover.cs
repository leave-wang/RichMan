using System;
using System.Collections;
using UnityEngine;

public class PlayerMover : MonoBehaviour
{
    [Header("Board")]
    public TileManager tileManager;

    [Header("Movement")]
    public float secondsPerTile = 0.25f;
    public float jumpHeight = 0.25f;
    public float rotateSpeed = 12f;

    public bool IsMoving { get; private set; }

    public void MoveSteps(
        int steps,
        Func<int> getCurrentIndex,
        Action<int> setCurrentIndex,
        Action onPassStart,
        Action onFinish)
    {
        if (IsMoving) return;

        StartCoroutine(
            MoveRoutine(
                steps,
                getCurrentIndex,
                setCurrentIndex,
                onPassStart,
                onFinish
            )
        );
    }

    private IEnumerator MoveRoutine(
        int steps,
        Func<int> getCurrentIndex,
        Action<int> setCurrentIndex,
        Action onPassStart,
        Action onFinish)
    {
        if (tileManager == null || tileManager.tiles == null || tileManager.tiles.Count == 0)
        {
            Debug.LogError("PlayerMover: tileManager tiles not ready.");
            yield break;
        }

        IsMoving = true;

        int count = tileManager.tiles.Count;

        for (int i = 0; i < steps; i++)
        {
            int current = getCurrentIndex();
            int next = (current + 1) % count;

            if (next == 0)
            {
                onPassStart?.Invoke();
            }

            Transform nextTile = tileManager.tiles[next];

            Vector3 startPosition = transform.position;
            Vector3 endPosition = nextTile.position + Vector3.up * 0.5f;

            Vector3 direction = endPosition - startPosition;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
                StartCoroutine(SmoothRotate(targetRotation, secondsPerTile));
            }

            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, secondsPerTile);

                float eased = EaseInOutCubic(Mathf.Clamp01(t));

                Vector3 position = Vector3.Lerp(startPosition, endPosition, eased);
                position.y += Mathf.Sin(eased * Mathf.PI) * jumpHeight;

                transform.position = position;

                yield return null;
            }

            transform.position = endPosition;

            setCurrentIndex(next);

            yield return new WaitForSeconds(0.03f);
        }

        IsMoving = false;

        onFinish?.Invoke();
    }

    private IEnumerator SmoothRotate(Quaternion targetRotation, float duration)
    {
        Quaternion startRotation = transform.rotation;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duration);

            float eased = EaseInOutCubic(Mathf.Clamp01(t));

            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);

            yield return null;
        }

        transform.rotation = targetRotation;
    }

    private float EaseInOutCubic(float x)
    {
        if (x < 0.5f)
        {
            return 4f * x * x * x;
        }

        return 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }
}