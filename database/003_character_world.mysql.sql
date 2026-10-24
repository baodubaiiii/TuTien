ALTER TABLE characters
    ADD CONSTRAINT fk_character_realm FOREIGN KEY(realm_id) REFERENCES realm_template(id),
    ADD CONSTRAINT fk_character_map FOREIGN KEY(map_id) REFERENCES map_template(id);
