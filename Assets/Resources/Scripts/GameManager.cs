using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

 
public class GameManager : MonoBehaviour
{
    [Header("References")]
    public RectTransform fallingBlock;       // the FallingBlock RectTransform
    public Image[] fallingBits;             // bit0–bit3 Images on FallingBlock
    public Image[] baseBits;               // bit0–bit7 Images on BaseRow
    public RectTransform playArea;          // PlayArea(Mask) RectTransform
    public TextMeshProUGUI scoreText;
 
    [Header("Colors")]
    public Color bitOnColor  = Color.white;
    public Color bitOffColor = new Color(0.15f, 0.15f, 0.15f);
 
    [Header("Layout")]
    public float bitSize     = 50f;   // width & height of each bit cell
    public float bitSpacing  = 4f;    // gap between cells
    public float fallSpeed   = 120f;  // pixels per second
    public float inputSpeed  = 200f;  // horizontal pixels per second
 
    [Header("Timing")]
    public float lockDelay   = 0.15f; // seconds before XOR fires after landing
 
    // ── internal state ──────────────────────────────────────────────
    private int[] _base     = new int[8];   // 8-bit base register
    private int[] _falling  = new int[4];   // 4-bit falling pattern
 
    private float _playAreaTop;             // y where block spawns (local to playArea)
    private float _playAreaBottom;          // y of base row (local to playArea)
 
    private float _blockY;                  // current local Y of falling block
    private float _blockX;                  // current local X (left edge of block)
 
    private float _minX;                    // leftmost valid X for block
    private float _maxX;                    // rightmost valid X for block
 
    private bool  _falling_active = false;
    private bool  _locking        = false;
 
    private int   _score = 0;
 
    // ── Unity lifecycle ──────────────────────────────────────────────
 
    void Start()
    {
        CalculateBounds();
        RandomiseBase();
        SpawnBlock();
    }
 
    void Update()
    {
        if (!_falling_active || _locking) return;
 
        HandleInput();
        ApplyGravity();
        CheckLanding();
        ApplyPosition();
    }
 
    // ── Setup ────────────────────────────────────────────────────────
    void RandomiseBase()
    {
        for (int i = 0; i < _base.Length; i++)
            _base[i] = Random.Range(0, 2);

        // Make sure it isn't already all 1s (instant win on load)
        while (System.Array.TrueForAll(_base, b => b == 1))
            _base[Random.Range(0, _base.Length)] = 0;

        RefreshBaseVisuals();
    }
 
    /// Work out the local-space Y limits inside the play area.
    void CalculateBounds()
    {
        // Size and position each falling bit
        float blockWidth = fallingBits.Length * bitSize + (fallingBits.Length - 1) * bitSpacing;
        fallingBlock.sizeDelta = new Vector2(blockWidth, bitSize);

        for (int i = 0; i < fallingBits.Length; i++)
        {
            RectTransform r = fallingBits[i].GetComponent<RectTransform>();
            r.sizeDelta        = new Vector2(bitSize, bitSize);
            r.anchoredPosition = new Vector2(i * (bitSize + bitSpacing), 0);
        }

        // Size and position each base bit
        RectTransform baseRect = baseBits[0].transform.parent.GetComponent<RectTransform>();
        float totalWidth = baseBits.Length * bitSize + (baseBits.Length - 1) * bitSpacing;
        baseRect.sizeDelta = new Vector2(totalWidth, bitSize);

        for (int i = 0; i < baseBits.Length; i++)
        {
            RectTransform r = baseBits[i].GetComponent<RectTransform>();
            r.sizeDelta        = new Vector2(bitSize, bitSize);
            r.anchoredPosition = new Vector2(i * (bitSize + bitSpacing), 0);
        }

        // Bounds in PlayArea local space
        // anchoredPosition moves the block's pivot, so clamp pivot X directly
        float playWidth = playArea.rect.width;
        _minX = GetBaseRowLeftEdge();
        _maxX = GetBaseRowLeftEdge() + (baseBits.Length - fallingBits.Length) * (bitSize + bitSpacing);

        float playHeight = playArea.rect.height;
        _playAreaTop    =  playHeight / 2f;
        _playAreaBottom = -playHeight / 2f;
    } 
    float GetBaseRowLeftEdge()
    {
        RectTransform baseRect = baseBits[0].transform.parent.GetComponent<RectTransform>();
        return baseRect.anchoredPosition.x - baseRect.rect.width * baseRect.pivot.x;
    }
    /// Randomise the 4-bit pattern and reset the block to the top.
[Header("Block Size")]
public int maxBlockBits = 4; // set to 4 in Inspector; range 1–4

    void SpawnBlock()
    {
        // Pick a random width 1–maxBlockBits
        int blockSize = Random.Range(1, maxBlockBits + 1);

        // Resize fallingBits array usage — hide unused bits
        for (int i = 0; i < fallingBits.Length; i++)
            fallingBits[i].gameObject.SetActive(i < blockSize);

        // Random pattern, at least one 1
        do {
            for (int i = 0; i < blockSize; i++)
                _falling[i] = Random.Range(0, 2);
        }
        while (System.Array.TrueForAll(_falling, b => b == 0)); // retry if all zeros

        // Zero out unused slots
        for (int i = blockSize; i < _falling.Length; i++)
            _falling[i] = 0;

        RefreshFallingVisuals();

        // Resize the FallingBlock panel to match actual bit count
        float newWidth = blockSize * bitSize + (blockSize - 1) * bitSpacing;
        fallingBlock.sizeDelta = new Vector2(newWidth, bitSize);

        // Recalculate horizontal bounds for new width
        float playWidth = playArea.rect.width;
        _minX = GetBaseRowLeftEdge();
        _maxX = GetBaseRowLeftEdge() + (baseBits.Length - blockSize) * (bitSize + bitSpacing);

        // Snap starting X to grid
        _blockX = _minX;
        _blockY = _playAreaTop + bitSize;

        _falling_active = true;
        _locking        = false;

        ApplyPosition();
    }

    // ── Input & movement ─────────────────────────────────────────────
 
[Header("Snap Movement")]
public float snapCooldown = 0.12f; // seconds between snaps (hold key repeat rate)
private float _snapTimer = 0f;
private bool _snapQueued = false;

    void HandleInput()
    {
        _snapTimer -= Time.deltaTime;

        bool leftPressed  = Keyboard.current.leftArrowKey.wasPressedThisFrame  || Keyboard.current.aKey.wasPressedThisFrame;
        bool rightPressed = Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame;
        bool leftHeld     = Keyboard.current.leftArrowKey.isPressed  || Keyboard.current.aKey.isPressed;
        bool rightHeld    = Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed;

        int dir = 0;
        if (leftPressed  || (leftHeld  && _snapTimer <= 0f)) dir = -1;
        if (rightPressed || (rightHeld && _snapTimer <= 0f)) dir =  1;

        if (dir != 0)
        {
            float step = bitSize + bitSpacing;
            _blockX = Mathf.Clamp(_blockX + dir * step, _minX, _maxX);
            _snapTimer = snapCooldown;
        }
    }

    void ApplyGravity()
    {
        _blockY -= fallSpeed * Time.deltaTime;
    }
 
    void CheckLanding()
    {
        float blockBottom = _blockY - bitSize;
 
        if (blockBottom <= _playAreaBottom && !_locking)
        {
            _blockY = _playAreaBottom + bitSize; // snap to floor
            StartCoroutine(LockAndXOR());
        }
    }
 
    void ApplyPosition()
    {
        fallingBlock.anchoredPosition = new Vector2(_blockX, _blockY);
    }
 
    // ── XOR logic ────────────────────────────────────────────────────
 
    IEnumerator LockAndXOR()
    {
        _locking        = true;
        _falling_active = false;
 
        yield return new WaitForSeconds(lockDelay);
 
        // Figure out which base bits align with the falling block.
        // Map block's left-edge X to a base bit index.
        int startIndex = XPositionToBaseIndex(_blockX);
 
        for (int i = 0; i < _falling.Length; i++)
        {
            int baseIdx = startIndex + i;
            if (baseIdx >= 0 && baseIdx < _base.Length)
                _base[baseIdx] ^= _falling[i];
        }
 
        RefreshBaseVisuals();
 
        if (CheckWin())
        {
            _score++;
            UpdateScore();
            StartCoroutine(WinFlash());
        }
        else
        {
            SpawnBlock();
        }
    }
 
    /// Convert a local-space X position to the nearest base bit index (0–7).
    int XPositionToBaseIndex(float localX)
    {
        float cellWidth = bitSize + bitSpacing;
        float relativeX = localX - GetBaseRowLeftEdge();
        int idx = Mathf.RoundToInt(relativeX / cellWidth);

        int activeSize = 0;
        foreach (var b in fallingBits) if (b.gameObject.activeSelf) activeSize++;

        return Mathf.Clamp(idx, 0, _base.Length - activeSize);
    }


    bool CheckWin()
    {
        foreach (int b in _base)
            if (b != 1) return false;
        return true;
    }
 
    // ── Visuals ──────────────────────────────────────────────────────
 
    void RefreshFallingVisuals()
    {
        for (int i = 0; i < fallingBits.Length; i++)
            fallingBits[i].color = _falling[i] == 1 ? bitOnColor : bitOffColor;
    }
 
    void RefreshBaseVisuals()
    {
        for (int i = 0; i < baseBits.Length; i++)
            baseBits[i].color = _base[i] == 1 ? bitOnColor : bitOffColor;
    }
 
    void UpdateScore()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {_score}";
    }
 
    // ── Win sequence ─────────────────────────────────────────────────
 
    IEnumerator WinFlash()
    {
        // Flash base row 3 times
        for (int flash = 0; flash < 3; flash++)
        {
            foreach (var img in baseBits) img.color = Color.yellow;
            yield return new WaitForSeconds(0.12f);
            RefreshBaseVisuals();
            yield return new WaitForSeconds(0.12f);
        }
 
        // Clear the base register
        System.Array.Clear(_base, 0, _base.Length);
        RefreshBaseVisuals();
 
        yield return new WaitForSeconds(0.2f);
 
        SpawnBlock();
    }
 
    // ── Debug helpers (visible in Inspector via context menu) ─────────
 
    [ContextMenu("Debug: Set Base to 11110000")]
    void DebugSetBase()
    {
        int[] test = { 1,1,1,1,0,0,0,0 };
        System.Array.Copy(test, _base, 8);
        RefreshBaseVisuals();
    }
 
    [ContextMenu("Debug: Print State")]
    void DebugPrintState()
    {
        Debug.Log($"Base:    {string.Join("", _base)}");
        Debug.Log($"Falling: {string.Join("", _falling)}");
        Debug.Log($"BlockX:  {_blockX:F1}  BlockY: {_blockY:F1}");
    }
}
