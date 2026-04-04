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
    public Image operationImage;   // the XOR banner image on the key
     public Image truthTableImage;   // the XOR banner image on the key
     public Image goalImage; 
     public GameObject startScreenBGDark;

 
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

    [Header("Scoring & Levels")]
    public TextMeshProUGUI levelText;
    public int   pointsPerBit    = 100;
    public int pointsPerTouch = 25;
    public float speedIncreasePerLevel = 20f;  // added to fallSpeed each level
    public float baseFallSpeed   = 120f;       // starting speed (match your fallSpeed value)

    [Header("Sprites")]
public Sprite fallingBitOn;   // sprite for falling block 1-bit
public Sprite fallingBitOff;  // sprite for falling block 0-bit
public Sprite baseBitOn;      // sprite for base row 1-bit
public Sprite baseBitOff;     // sprite for base row 0-bit

    private int _level = 1;
    private bool _debugOverride = false;



    public enum BitOperation { XOR, AND, NAND }
    private BitOperation _currentOperation = (BitOperation)(-1); // no operation yet

 
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

    if (_debugOverride)
    {
        _blockY = _playAreaTop + bitSize;
        _falling_active = true;
        ApplyPosition();
    }
    else
    {
        RandomiseBase();
        SpawnBlock();
        PickRandomOperation();
        UpdateLevelText();
    }
}


    void Update()
{
    if (startScreenBGDark != null && startScreenBGDark.activeSelf)
    {
        Time.timeScale = 0f;
        return;
    } else{
        Time.timeScale = 1f;
    }

    

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
            r.anchorMin        = new Vector2(0f, 0.5f);
            r.anchorMax        = new Vector2(0f, 0.5f);
            r.pivot            = new Vector2(0f, 0.5f);
            r.sizeDelta        = new Vector2(bitSize, bitSize);
            r.anchoredPosition = new Vector2(i * (bitSize + bitSpacing), 0f);
        }

        fallingBlock.anchorMin = new Vector2(0.5f, 0.5f);
        fallingBlock.anchorMax = new Vector2(0.5f, 0.5f);
        fallingBlock.pivot     = new Vector2(0f, 0.5f);

        // Size and position each base bit
        RectTransform baseRect = baseBits[0].transform.parent.GetComponent<RectTransform>();
        float totalWidth = baseBits.Length * bitSize + (baseBits.Length - 1) * bitSpacing;
        baseRect.sizeDelta = new Vector2(totalWidth, bitSize);

        // Anchor BaseRow to center, then position bits from its local left edge
        baseRect.anchorMin = new Vector2(0.5f, 0.5f);
        baseRect.anchorMax = new Vector2(0.5f, 0.5f);
        baseRect.pivot     = new Vector2(0f, 0.5f); // pivot at LEFT edge
        baseRect.anchoredPosition = new Vector2(-totalWidth / 2f, baseRect.anchoredPosition.y);

        for (int i = 0; i < baseBits.Length; i++)
        {
            RectTransform r = baseBits[i].GetComponent<RectTransform>();
            r.anchorMin        = new Vector2(0f, 0.5f);
            r.anchorMax        = new Vector2(0f, 0.5f);
            r.pivot            = new Vector2(0f, 0.5f);
            r.sizeDelta        = new Vector2(bitSize, bitSize);
            r.anchoredPosition = new Vector2(i * (bitSize + bitSpacing), 0f);
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

    void SpawnBlock(){
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

    void PickRandomOperation()
{
    BitOperation next;
    do {
        next = (BitOperation)Random.Range(0, 3);
    }
    while (next == _currentOperation);

    _currentOperation = next;
    UpdateOperationVisuals();
}


    void UpdateOperationVisuals()
{
    if (operationImage != null)
    {
        Sprite spr = Resources.Load<Sprite>($"png/{_currentOperation}");
        if (spr != null) operationImage.sprite = spr;
    }

    if (truthTableImage != null)
    {
        Sprite spr = Resources.Load<Sprite>($"png/{_currentOperation}tt");
        if (spr != null) truthTableImage.sprite = spr;
    }

    if (goalImage != null)
    {
        string goalName = _currentOperation == BitOperation.AND ? "goalAND" : "goal";
        Sprite spr = Resources.Load<Sprite>($"png/{goalName}");
        if (spr != null) goalImage.sprite = spr;
    }
}





    // ── Input & movement ─────────────────────────────────────────────
 
[Header("Snap Movement")]
public float snapCooldown = 0.12f; // seconds between snaps (hold key repeat rate)
private float _snapTimer = 0f;
private bool _snapQueued = false;

    void HandleInput()
    {
        bool dropPressed = Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame;

        if (dropPressed)
        {
            StartCoroutine(DropAndLock());
            return;
        }
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


    // Add this new method:
    IEnumerator DropAndLock()
    {
        _falling_active = false;
        _locking        = true;  // ← add this
        _blockX = SnapToGrid(_blockX);  // ← snap before locking
        _blockY = _playAreaBottom + bitSize;
        ApplyPosition();
        yield return new WaitForSeconds(0.2f);
        StartCoroutine(LockAndXOR());
    }   

    float SnapToGrid(float x)
    {
        float cellWidth = bitSize + bitSpacing;
        float relative  = x - GetBaseRowLeftEdge();
        int   cell      = Mathf.RoundToInt(relative / cellWidth);
        return GetBaseRowLeftEdge() + cell * cellWidth;
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

        int startIndex = XPositionToBaseIndex(_blockX);
int gained = 0, lost = 0, unchanged = 0;

int activeSize = 0;
foreach (var b in fallingBits) if (b.gameObject.activeSelf) activeSize++;

for (int i = 0; i < activeSize; i++)  // ← activeSize not _falling.Length
{
    int baseIdx = startIndex + i;
    if (baseIdx < 0 || baseIdx >= _base.Length) continue;

    int a = _base[baseIdx];
    int b = _falling[i];
    int result;

    switch (_currentOperation)
    {
        case BitOperation.XOR:  result = a ^ b;        break;
        case BitOperation.AND:  result = a & b;        break;
        case BitOperation.NAND: result = 1 - (a & b); break;
        default:                result = a ^ b;        break;
    }

    if (result != a)
    {
        if (result == 1) gained++;
        else             lost++;
    }
    else unchanged++;

    _base[baseIdx] = result;
}


int delta = (gained * pointsPerBit) - (lost * pointsPerBit) + (unchanged * pointsPerTouch);
_score = Mathf.Max(0, _score + delta);

        UpdateScore();

        RefreshBaseVisuals();

        if (CheckWin())
        {
            _level++;
            fallSpeed = baseFallSpeed + (_level - 1) * speedIncreasePerLevel;
            UpdateLevelText();
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
        int target = _currentOperation == BitOperation.AND ? 0 : 1;
        foreach (int b in _base)
            if (b != target) return false;
        return true;
    } 


    // ── Visuals ──────────────────────────────────────────────────────
 
    void RefreshFallingVisuals()
    {
        for (int i = 0; i < fallingBits.Length; i++){
            fallingBits[i].color  = Color.white;
            fallingBits[i].sprite = _falling[i] == 1 ? fallingBitOn : fallingBitOff;
        }
        
    }

    void RefreshBaseVisuals()
    {
        for (int i = 0; i < baseBits.Length; i++){
            baseBits[i].color  = Color.white;
            baseBits[i].sprite = _base[i] == 1 ? baseBitOn : baseBitOff;
        }

    } 
   void UpdateScore()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {_score}";
    }

    void UpdateLevelText()
    {
        if (levelText != null)
            levelText.text = $"Level: {_level}";
    }
 
    // ── Win sequence ─────────────────────────────────────────────────
 
    IEnumerator WinFlash()
    {
        for (int flash = 0; flash < 3; flash++)
        {
            foreach (var img in baseBits) img.color = Color.yellow;
            yield return new WaitForSeconds(0.12f);
            foreach (var img in baseBits) img.color = Color.white;  // ← add this
            yield return new WaitForSeconds(0.12f);
        }

        // Generate a new random base that isn't all 1s (that would instant-win again)
        do {
            for (int i = 0; i < _base.Length; i++)
                _base[i] = Random.Range(0, 2);
        }
        while (System.Array.TrueForAll(_base, b => b == 1));

        RefreshBaseVisuals();
        PickRandomOperation();

        yield return new WaitForSeconds(0.5f);

        SpawnBlock();
    }

    // ── Debug helpers (visible in Inspector via context menu) ─────────
 
[ContextMenu("Debug: Force Near Win")]
void DebugForceNearWin()
{
    _debugOverride = true;

    int[] test = { 1,1,1,1,1,1,1,0 };
    System.Array.Copy(test, _base, 8);
    RefreshBaseVisuals();

    for (int i = 0; i < fallingBits.Length; i++)
        fallingBits[i].gameObject.SetActive(i == 0);
    _falling[0] = 1;
    for (int i = 1; i < _falling.Length; i++)
        _falling[i] = 0;

    RefreshFallingVisuals();

    float cellWidth = bitSize + bitSpacing;
    fallingBlock.sizeDelta = new Vector2(bitSize, bitSize);

    _minX = GetBaseRowLeftEdge();
    _maxX = GetBaseRowLeftEdge() + (baseBits.Length - 1) * cellWidth;

    _blockX = GetBaseRowLeftEdge() + 7 * cellWidth;
    _blockY = _playAreaTop + bitSize;

    _falling_active = true;
    _locking        = false;

    ApplyPosition();
}


    [ContextMenu("Debug: Print State")]
    void DebugPrintState()
    {
        Debug.Log($"Base:    {string.Join("", _base)}");
        Debug.Log($"Falling: {string.Join("", _falling)}");
        Debug.Log($"BlockX:  {_blockX:F1}  BlockY: {_blockY:F1}");
    }
}
