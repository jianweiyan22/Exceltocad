;;; Excel2GuanLiDe.lsp
;;; AutoCAD 2018+ / 管立得辅助工具
;;; 命令：E2GDNODE
;;; 功能：读取 Excel2GuanLiDe 导出的节点 CSV，并按真实桩号距离生成 CAD 节点与文字。

(vl-load-com)

(defun e2gd:split (s sep / p out)
  (setq out '())
  (while (setq p (vl-string-search sep s))
    (setq out (cons (substr s 1 p) out))
    (setq s (substr s (+ p (strlen sep) 1)))
  )
  (reverse (cons s out))
)

(defun e2gd:unquote (s)
  (if (and (> (strlen s) 1) (= (substr s 1 1) "\"") (= (substr s (strlen s) 1) "\""))
    (substr s 2 (- (strlen s) 2))
    s
  )
)

(defun e2gd:num (s / v)
  (setq s (e2gd:unquote s))
  (if (= s "") nil (atof s))
)

(defun e2gd:addtext (pt txt h rot / obj)
  (if (and txt (/= txt ""))
    (progn
      (setq obj (entmakex
        (list '(0 . "TEXT")
              '(100 . "AcDbEntity")
              '(100 . "AcDbText")
              (cons 10 pt)
              (cons 40 h)
              (cons 1 txt)
              (cons 50 rot)
              '(7 . "Standard")
        )
      ))
    )
  )
)

(defun c:E2GDNODE (/ fn f line fields id station st ge pe pressure base scale h row x y p)
  (prompt "\nExcel2GuanLiDe：选择“管立得_节点数据.csv”... ")
  (setq fn (getfiled "选择节点 CSV" "" "csv" 0))
  (if (not fn)
    (progn (prompt "\n已取消。") (princ))
    (progn
      (setq base (getpoint "\n指定起始桩号 K0+000 的 CAD 点："))
      (setq scale (getreal "\n输入出图比例（例如 2000）："))
      (if (or (not scale) (<= scale 0.0)) (setq scale 2000.0))
      (setq h (getreal "\n输入文字高度（例如 3.0）："))
      (if (or (not h) (<= h 0.0)) (setq h 3.0)
      )
      (setq f (open fn "r"))
      (if (not f)
        (prompt "\n无法打开 CSV 文件。")
        (progn
          ;; 跳过表头
          (read-line f)
          (setq row 0)
          (while (setq line (read-line f))
            (setq fields (e2gd:split line ","))
            (if (>= (length fields) 6)
              (progn
                (setq id (e2gd:unquote (nth 0 fields)))
                (setq station (e2gd:num (nth 1 fields)))
                (setq st (e2gd:unquote (nth 2 fields)))
                (setq ge (e2gd:num (nth 3 fields)))
                (setq pe (e2gd:num (nth 4 fields)))
                (setq pressure (e2gd:unquote (nth 5 fields)))
                (if station
                  (progn
                    ;; station 为米；CAD 图上按 1:scale 换算为毫米
                    (setq x (+ (car base) (* station 1000.0 (/ 1.0 scale))))
                    (setq y (cadr base))
                    (setq p (list x y 0.0))
                    (entmakex (list '(0 . "POINT") (cons 10 p)))
                    (e2gd:addtext (list x (+ y (* h 3.0)) 0.0) st h (/ pi 2.0))
                    (if ge
                      (e2gd:addtext (list x (- y (* h 3.0)) 0.0) (rtos ge 2 3) h (/ pi 2.0))
                    )
                    (if pe
                      (e2gd:addtext (list x (- y (* h 6.0)) 0.0) (strcat "管底 " (rtos pe 2 3)) h (/ pi 2.0))
                    )
                    (if pressure
                      (e2gd:addtext (list x (+ y (* h 6.0)) 0.0) (strcat "水压 " pressure) h (/ pi 2.0))
                    )
                    (setq row (1+ row))
                  )
                )
              )
            )
          )
          (close f)
          (prompt (strcat "\n完成：已生成 " (itoa row) " 个节点。"))
        )
      )
      (princ)
    )
  )
)

(princ "\nExcel2GuanLiDe 已加载。输入 E2GDNODE 开始生成节点。\n")
(princ)
