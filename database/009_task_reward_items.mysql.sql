ALTER TABLE task_template
    ADD COLUMN IF NOT EXISTS reward_tu_vi BIGINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS reward_points INT NOT NULL DEFAULT 0;

UPDATE task_template
SET reward_tu_vi = COALESCE(JSON_VALUE(reward, '$.tuVi'), 0),
    reward_points = COALESCE(JSON_VALUE(reward, '$.points'), 0),
    reward = CASE
        WHEN COALESCE(JSON_VALUE(reward, '$.itemId'), 0) > 0 THEN
            JSON_ARRAY(JSON_OBJECT(
                'id', CAST(JSON_VALUE(reward, '$.itemId') AS UNSIGNED),
                'quantity', CAST(JSON_VALUE(reward, '$.quantity') AS UNSIGNED),
                'options', JSON_EXTRACT(COALESCE((
                    SELECT item_template.options
                    FROM item_template
                    WHERE item_template.id = JSON_VALUE(task_template.reward, '$.itemId')
                ), '[]'), '$')
            ))
        ELSE JSON_ARRAY()
    END
WHERE JSON_TYPE(reward) = 'OBJECT';

INSERT IGNORE INTO tutien_schema(id, name) VALUES(7, 'Task reward List<GameItem>');
