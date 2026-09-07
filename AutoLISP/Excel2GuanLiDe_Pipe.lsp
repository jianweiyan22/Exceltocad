;;; Excel2GuanLiDe_Pipe.lsp
;;; 将节点 CSV 按桩号顺序连接成管线中心线。
;;; 命令：E2GDPIPE

(defun e2gd:p-split (s sep / p out)
  (setq out '())
  (while (setq p (vl-string-search sep s))
    (setq out (cons (substr s 1 p) out))
    (setq s (substr s (+ p (strlen sep) 1)))
  )
  (reverse (cons s out))
)
(defun e2gd:p-unq (s)
  (if (and (> (strlen s) 1) (= (substr s 1 1) "\"") (= (substr s (strlen s) 1) "\""))
    (substr s 2 (- (strlen s) 2)) s))
(defun e2gd:p-num (s) (if (= (e2gd:p-unq s) "") nil (atof (e2gd:p-unq s))))

(defun c:E2GDPIPE (/ fn f line fs station lastst base scale y p pts n)
  (setq fn (getfiled "选择节点 CSV" "" "csv" 0))
  (if fn
    (progn
      (setq base (getpoint "\n指定 K0+000 起点："))
      (setq scale (getreal "\n输入出图比例（例如 2000）："))
      (if (or (not scale) (<= scale 0.0)) (setq scale 2000.0))
      (setq y (cadr base) pts '())
      (setq f (open fn "r"))
      (if f
        (progn
          (read-line f)
          (while (setq line (read-line f))
            (setq fs (e2gd:p-split line ","))
            (if (>= (length fs) 3)
              (progn
                (setq station (e2gd:p-num (nth 1 fs)))
                (if station
                  (setq pts (cons (list (+ (car base) (* station 1000.0 (/ 1.0 scale))) y 0.0) pts))
                )
              )
            )
          )
          (close f)
          (setq pts (reverse pts))
          (if (> (length pts) 1)
            (progn
              (entmakex
                (append
                  (list '(0 . "LWPOLYLINE") '(100 . "AcDbEntity") '(100 . "AcDbPolyline")
                        (cons 90 (length pts)) '(70 . 0))
                  (mapcar '(lambda (q) (cons 10 (list (car q) (cadr q)))) pts)))
              (prompt (strcat "\n已生成连续管线中心线，共 " (itoa (length pts)) " 个节点。"))
            )
            (prompt "\n节点不足 2 个，无法生成管线。")
          )
        )
      )
    )
  )
  (princ)
)
(princ "\nExcel2GuanLiDe 管线命令已加载：E2GDPIPE\n")
(princ)
