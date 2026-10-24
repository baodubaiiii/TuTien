ALTER TABLE task_template ADD COLUMN IF NOT EXISTS reward JSON NULL;

SET @task_reward_sql = IF(
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'task_template'
       AND column_name IN ('reward_tu_vi', 'reward_points', 'reward_item_id', 'reward_quantity')) = 4,
    'UPDATE task_template SET reward = JSON_OBJECT(''tuVi'', reward_tu_vi, ''points'', reward_points, ''itemId'', reward_item_id, ''quantity'', reward_quantity) WHERE reward IS NULL',
    'SELECT 1'
);

PREPARE task_reward_statement FROM @task_reward_sql;
EXECUTE task_reward_statement;
DEALLOCATE PREPARE task_reward_statement;

ALTER TABLE task_template MODIFY COLUMN reward JSON NOT NULL;
ALTER TABLE task_template
    DROP COLUMN IF EXISTS reward_tu_vi,
    DROP COLUMN IF EXISTS reward_points,
    DROP COLUMN IF EXISTS reward_item_id,
    DROP COLUMN IF EXISTS reward_quantity;

INSERT IGNORE INTO tutien_schema(id, name) VALUES(6, 'Task reward JSON');
