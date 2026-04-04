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
 
    /// Work out the local-space Y limits inside the play area.
    void CalculateBounds()
    {
        float h = playArea.rect.height;
        float w = playArea.rect.width;
 
        // PlayArea pivot is assumed 0.5,0.5 (center).
        // Local Y ranges from -h/2 (bottom) to +h/2 (top).
        _playAreaTop    =  h / 2f;
        _playAreaBottom = -h / 2f;
 
        float blockWidth = fallingBits.Length * bitSize + (fallingBits.Length - 1) * bitSpacing;
 
        _minX = -w / 2f;
        _maxX =  w / 2f - blockWidth;
    }
 
    /// Randomise the 4-bit pattern and reset the block to the top.
    void SpawnBlock()
    {
        // Random 4-bit pattern — at least one bit is 1 so it's never a no-op
        do
        {
            for (int i = 0; i < _falling.Length; i++)
                _falling[i] = Random.Range(0, 2);
        }
        while (System.Array.TrueForAll(_falling, b => b == 0));
 
        RefreshFallingVisuals();
 
        // Start just above the visible area
        _blockY = _playAreaTop + bitSize;
        _blockX = _minX;
 
        _falling_active = true;
        _locking        = false;
 
        ApplyPosition();
    }
 
    // ── Input & movement ─────────────────────────────────────────────
 
    void HandleInput()
    {
        float move = 0f;

        if (Keyboard.current.leftArrowKey.isPressed  || Keyboard.current.aKey.isPressed) move = -1f;
        if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) move =  1f;

        _blockX += move * inputSpeed * Time.deltaTime;
        _blockX  = Mathf.Clamp(_blockX, _minX, _maxX);
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
        float playWidth = playArea.rect.width;
        float leftEdge  = -playWidth / 2f;
        float cellWidth = bitSize + bitSpacing;
 
        // Which cell does the left edge of the block fall closest to?
        int idx = Mathf.RoundToInt((localX - leftEdge) / cellWidth);
        return Mathf.Clamp(idx, 0, _base.Length - _falling.Length);
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
